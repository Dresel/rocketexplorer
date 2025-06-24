using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using RocketExplorer.Ethereum.RocketMegapoolDelegate;
using RocketExplorer.Ethereum.RocketMegapoolDelegate.ContractDefinition;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

internal class MegapoolEventHandlers
{
	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorAssignedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address.ConvertToEthereumChecksumAddress();
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			context, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Staking,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		int h = 0;

		// TODO: Sequence Equal?
		h += context.QueueInfo.StandardQueue.RemoveAll(x =>
			x.PubKey == context.ValidatorInfo.Partial.UpdatedMegapoolValidators[megapoolAddress][validatorId].PubKey);
		h += context.QueueInfo.ExpressQueue.RemoveAll(x =>
			x.PubKey == context.ValidatorInfo.Partial.UpdatedMegapoolValidators[megapoolAddress][validatorId].PubKey);

		Debug.Assert(h == 1, "Only one element should be removed");

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(time).DateTime);
		context.QueueInfo.DailyDequeued[key] = context.QueueInfo.DailyDequeued.GetValueOrDefault(key) + 1;
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() - 1;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorDequeuedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string megapoolAddress = eventLog.Log.Address.ConvertToEthereumChecksumAddress();
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			context, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Dequeued,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		int h = 0;

		// TODO: Sequence Equal?
		h += context.QueueInfo.StandardQueue.RemoveAll(x =>
			x.PubKey == context.ValidatorInfo.Partial.UpdatedMegapoolValidators[megapoolAddress][validatorId].PubKey);
		h += context.QueueInfo.ExpressQueue.RemoveAll(x =>
			x.PubKey == context.ValidatorInfo.Partial.UpdatedMegapoolValidators[megapoolAddress][validatorId].PubKey);

		Debug.Assert(h == 1, "Only one element should be removed");

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(time).DateTime);
		context.QueueInfo.DailyVoluntaryExits[key] = context.QueueInfo.DailyVoluntaryExits.GetValueOrDefault(key) + 1;
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() - 1;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorEnqueuedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string megapoolAddress = eventLog.Log.Address.ConvertToEthereumChecksumAddress();
		string? nodeOperatorAddress = await TryGetNodeOperator(context, megapoolAddress, eventLog.Log, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		// First enqueued validator for this megapool address
		if (context.Nodes.Data.Index[nodeOperatorAddress].MegapoolAddress == null)
		{
			context.Nodes.Data.Index[nodeOperatorAddress] = context.Nodes.Data.Index[nodeOperatorAddress] with
			{
				MegapoolAddress = megapoolAddress.HexToByteArray(),
			};

			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				context.Nodes.Partial.Updated[nodeOperatorAddress] with
				{
					MegapoolAddress = megapoolAddress.HexToByteArray(),
				};
		}

		RocketMegapoolDelegateService megapoolDelegate = new(context.Web3, megapoolAddress);
		GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(
			(uint)eventLog.Event.ValidatorId, new BlockParameter(eventLog.Log.BlockNumber));

		MegapoolValidatorIndexEntry entry = new()
		{
			NodeAddress = nodeOperatorAddress.HexToByteArray(),
			MegapoolAddress = megapoolAddress.HexToByteArray(),
			MegapoolIndex = (int)eventLog.Event.ValidatorId,
			PubKey = validatorInfo.ReturnValue1.PubKey,
		};

		Validator validator = new()
		{
			NodeAddress = entry.NodeAddress,
			MegapoolAddress = entry.MegapoolAddress,
			MegapoolIndex = entry.MegapoolIndex,
			PubKey = entry.PubKey,
			ExpressTicketUsed = validatorInfo.ReturnValue1.ExpressUsed,
			Status = ValidatorStatus.Created,
			Bond = 4, // TODO: Saturn2
			Type = ValidatorType.Megapool,
			History =
			[
				new ValidatorHistory
				{
					Status = ValidatorStatus.Created,
					Timestamp = (long)eventLog.Event.Time,
				},
			],
		};

		context.ValidatorInfo.Partial.UpdatedMegapoolValidators.TryAdd(megapoolAddress, []);
		Dictionary<int, Validator> megapoolValidators =
			context.ValidatorInfo.Partial.UpdatedMegapoolValidators[megapoolAddress];

		megapoolValidators.Add(validator.MegapoolIndex.Value, validator);

		// TODO: Use list
		context.Nodes.Partial.Updated[nodeOperatorAddress].MegapoolValidators =
		[
			..context.Nodes.Partial.Updated[nodeOperatorAddress].MegapoolValidators, entry,
		];

		if (!validatorInfo.ReturnValue1.ExpressUsed)
		{
			context.QueueInfo.StandardQueue.Add(entry);
		}
		else
		{
			context.QueueInfo.ExpressQueue.Add(entry);
		}

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() + 1;
		context.QueueInfo.DailyEnqueued[key] = context.QueueInfo.DailyEnqueued.GetValueOrDefault(key) + 1;
	}

	private static async Task<string?> EventMegapoolValidatorUpdateAsync(
		NodesSyncContext context, MegapoolUpdatedEvent updatedEvent, CancellationToken cancellationToken)
	{
		string? nodeOperatorAddress = await TryGetNodeOperator(
			context, updatedEvent.MegapoolAddress, updatedEvent.Log, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return null;
		}

		context.ValidatorInfo.Partial.UpdatedMegapoolValidators.TryAdd(updatedEvent.MegapoolAddress, []);

		Dictionary<int, Validator> megapoolValidators =
			context.ValidatorInfo.Partial.UpdatedMegapoolValidators[updatedEvent.MegapoolAddress];

		if (!megapoolValidators.ContainsKey(updatedEvent.ValidatorId))
		{
			megapoolValidators[updatedEvent.ValidatorId] = (await context.Storage.ReadAsync<Validator>(
					Keys.MegapoolValidator(updatedEvent.MegapoolAddress, updatedEvent.ValidatorId), cancellationToken))
				?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		megapoolValidators[updatedEvent.ValidatorId] = megapoolValidators[
				updatedEvent.ValidatorId] with
			{
				Status = updatedEvent.Status,
				History =
				[
					.. megapoolValidators[
						updatedEvent.ValidatorId].History,
					new ValidatorHistory
					{
						Status = updatedEvent.Status,
						Timestamp = updatedEvent.Time,
					},
				],
			};

		return nodeOperatorAddress;
	}

	private static async Task<string?> FetchNodeOperatorAddressFromMegapoolAddress(
		NodesSyncContext context, HexBigInteger blockNumber, string megapoolAddress,
		RocketMegapoolDelegateService megapoolDelegate, CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress =
			await megapoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(blockNumber));

		// If not found might be megapool from different rocket pool version
		if (!context.Nodes.Data.Index.ContainsKey(nodeOperatorAddress))
		{
			context.Logger.LogDebug(
				"Node operator {NodeOperatorAddress} for {MegapoolAddress} not found in index.", nodeOperatorAddress,
				megapoolAddress);
			return null;
		}

		try
		{
			// Can happen if the same node operator address is used for multiple rocket pool deployments
			if (!string.Equals(
					await context.RocketNodeManager.GetMegapoolAddressQueryAsync(nodeOperatorAddress), megapoolAddress,
					StringComparison.OrdinalIgnoreCase))
			{
				context.Logger.LogDebug(
					"Node operator {NodeOperatorAddress} found in index but megapool address {MegapoolAddress} does not match.",
					nodeOperatorAddress, megapoolAddress);
				return null;
			}
		}
		catch (Exception e)
		{
			// Not implemented, cannot rely on version query
			return null;
		}

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await context.Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		return nodeOperatorAddress;
	}

	private static async Task<string?> TryGetNodeOperator(
		NodesSyncContext context, string megapoolAddress, FilterLog filterLog,
		CancellationToken cancellationToken)
	{
		RocketMegapoolDelegateService megapoolDelegate = new(context.Web3, megapoolAddress);
		context.ValidatorInfo.Cache.MegapoolNodeOperatorMap.TryGetValue(
			megapoolAddress, out string? nodeOperatorAddress);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			nodeOperatorAddress = await FetchNodeOperatorAddressFromMegapoolAddress(
				context, filterLog.BlockNumber, megapoolAddress, megapoolDelegate, cancellationToken);

			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
			{
				return null;
			}

			context.ValidatorInfo.Cache.MegapoolNodeOperatorMap[megapoolAddress] = nodeOperatorAddress;
		}

		return nodeOperatorAddress;
	}

	internal class MegapoolUpdatedEvent
	{
		public required FilterLog Log { get; set; }

		public required string MegapoolAddress { get; set; }

		public required ValidatorStatus Status { get; set; }

		public required long Time { get; set; }

		public required int ValidatorId { get; set; }
	}
}