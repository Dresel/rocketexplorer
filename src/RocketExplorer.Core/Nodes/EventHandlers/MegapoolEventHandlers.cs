using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Ethereum.RocketMegapoolDelegate;
using RocketExplorer.Ethereum.RocketMegapoolDelegate.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class MegapoolEventHandlers
{
	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorAssignedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			context, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.PreLaunch,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		context.DashboardInfo.QueueLength--;

		int h = 0;

		h += context.QueueInfo.MegapoolStandardQueue.RemoveAll(x =>
			x.PubKey.SequenceEqual(
				context.ValidatorInfo.Partial.UpdatedMegapoolValidators[(megapoolAddress, validatorId)].PubKey ?? []));
		h += context.QueueInfo.MegapoolExpressQueue.RemoveAll(x =>
			x.PubKey.SequenceEqual(
				context.ValidatorInfo.Partial.UpdatedMegapoolValidators[(megapoolAddress, validatorId)].PubKey ?? []));

		Debug.Assert(h == 1, "Only one element should be removed");

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(time).DateTime);
		context.QueueInfo.DailyDequeued[key] = context.QueueInfo.DailyDequeued.GetValueOrDefault(key) + 1;
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestValueOrDefault() - 1;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorStakedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
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

		RocketMegapoolDelegateService megapoolDelegate = new(context.Web3, megapoolAddress);

		GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(
			(uint)validatorId, new BlockParameter(eventLog.Log.BlockNumber));

		context.ValidatorInfo.Data.MegapoolValidatorIndex[(megapoolAddress, validatorId)] =
			context.ValidatorInfo.Data.MegapoolValidatorIndex[(megapoolAddress, validatorId)] with
			{
				ValidatorIndex = (long)validatorInfo.ReturnValue1.ValidatorIndex,
			};

		context.ValidatorInfo.Partial.UpdatedMegapoolValidators[(megapoolAddress, validatorId)] =
			context.ValidatorInfo.Partial.UpdatedMegapoolValidators[(megapoolAddress, validatorId)] with
			{
				ValidatorIndex = (long)validatorInfo.ReturnValue1.ValidatorIndex,
			};

		context.DashboardInfo.MegapoolValidatorsStaking++;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorDissolvedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			context, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Dissolved,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorExitingEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			context, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Exiting,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		context.DashboardInfo.MegapoolValidatorsStaking--;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorExitedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			context, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Exited,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
		}
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorDequeuedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string megapoolAddress = eventLog.Log.Address;
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

		context.DashboardInfo.QueueLength--;

		int h = 0;

		h += context.QueueInfo.MegapoolStandardQueue.RemoveAll(x =>
			x.PubKey.SequenceEqual(
				context.ValidatorInfo.Partial.UpdatedMegapoolValidators[(megapoolAddress, validatorId)].PubKey ?? []));
		h += context.QueueInfo.MegapoolExpressQueue.RemoveAll(x =>
			x.PubKey.SequenceEqual(
				context.ValidatorInfo.Partial.UpdatedMegapoolValidators[(megapoolAddress, validatorId)].PubKey ?? []));

		Debug.Assert(h == 1, "Only one element should be removed");

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(time).DateTime);
		context.QueueInfo.DailyVoluntaryExits[key] = context.QueueInfo.DailyVoluntaryExits.GetValueOrDefault(key) + 1;
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestValueOrDefault() - 1;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MegapoolValidatorEnqueuedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string megapoolAddress = eventLog.Log.Address;
		int eventValidatorId = (int)eventLog.Event.ValidatorId;

		KeyValuePair<string, NodeIndexEntry>? indexEntry = context.Nodes.Data.Index
			.Where(node => node.Value.MegapoolAddress?.SequenceEqual(eventLog.Log.Address.HexToByteArray()) == true)
			.Cast<KeyValuePair<string, NodeIndexEntry>?>().SingleOrDefault();
		string? nodeOperatorAddress = indexEntry?.Value.ContractAddress.ToHex(true);

		if (indexEntry == null)
		{
			nodeOperatorAddress = await TryGetNodeOperator(context, megapoolAddress, eventLog.Log, cancellationToken);
		}

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		context.DashboardInfo.QueueLength++;

		RocketMegapoolDelegateService megapoolDelegate = new(context.Web3, megapoolAddress);

		byte[] pubKey = await megapoolDelegate.GetValidatorPubkeyQueryAsync(
			(uint)eventValidatorId, new BlockParameter(eventLog.Log.BlockNumber));

		Debug.Assert(pubKey.Length > 0, "PubKey should not be empty");

		MegapoolValidatorIndexEntry entry = new()
		{
			NodeAddress = nodeOperatorAddress.HexToByteArray(),
			MegapoolAddress = megapoolAddress.HexToByteArray(),
			MegapoolIndex = eventValidatorId,
			PubKey = pubKey,
			ValidatorIndex = null,
		};

		await context.GlobalIndexService.AddOrUpdateEntryAsync(
			megapoolAddress.HexToByteArray(),
			megapoolAddress.RemoveHexPrefix(),
			x =>
			{
				x.Type |= IndexEntryType.Megapool;
			}, cancellationToken);

		await context.GlobalIndexService.AddOrUpdateEntryAsync(
			megapoolAddress.HexToByteArray(), pubKey.ToHex(),
			x =>
			{
				x.Type |= IndexEntryType.MegapoolValidator;
				x.ValidatorPubKey = pubKey;
				x.MegapoolIndex = eventValidatorId;
			}, cancellationToken);

		context.ValidatorInfo.Data.MegapoolValidatorIndex.Add(
			(megapoolAddress, (int)eventLog.Event.ValidatorId), entry);

		context.Nodes.Data.Index[nodeOperatorAddress] = context.Nodes.Data.Index[nodeOperatorAddress] with
		{
			MegapoolAddress = megapoolAddress.HexToByteArray(),
		};

		GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(
			(uint)eventValidatorId, new BlockParameter(eventLog.Log.BlockNumber));

		Validator validator = new()
		{
			NodeAddress = entry.NodeAddress,
			MegapoolAddress = entry.MegapoolAddress,
			MegapoolIndex = entry.MegapoolIndex,
			PubKey = entry.PubKey,
			ValidatorIndex = entry.ValidatorIndex,
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

		context.ValidatorInfo.Partial.UpdatedMegapoolValidators.Add((megapoolAddress, eventValidatorId), validator);

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await context.Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		// TODO: Use list
		context.Nodes.Partial.Updated[nodeOperatorAddress] =
			context.Nodes.Partial.Updated[nodeOperatorAddress] with
			{
				MegapoolAddress = megapoolAddress.HexToByteArray(),
				MegapoolValidators =
				[
					..context.Nodes.Partial.Updated[nodeOperatorAddress].MegapoolValidators, entry,
				],
			};

		context.QueueInfo.MegapoolQueueIndex++;

		MegapoolValidatorQueueEntry queueEntry = new()
		{
			NodeAddress = entry.NodeAddress,
			MegapoolAddress = entry.MegapoolAddress,
			MegapoolIndex = entry.MegapoolIndex,
			PubKey = entry.PubKey,
			EnqueueTimestamp = (long)eventLog.Event.Time,
		};

		if (!validatorInfo.ReturnValue1.ExpressUsed)
		{
			context.QueueInfo.MegapoolStandardQueue.Add(queueEntry);
		}
		else
		{
			context.QueueInfo.MegapoolExpressQueue.Add(queueEntry);
		}

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestValueOrDefault() + 1;
		context.QueueInfo.DailyEnqueued[key] = context.QueueInfo.DailyEnqueued.GetValueOrDefault(key) + 1;
	}

	private static async Task<string?> EventMegapoolValidatorUpdateAsync(
		NodesSyncContext context, MegapoolUpdatedEvent updatedEvent, CancellationToken cancellationToken)
	{
		if (!context.ValidatorInfo.Data.MegapoolValidatorIndex.ContainsKey(
				(updatedEvent.MegapoolAddress, updatedEvent.ValidatorId)))
		{
			return null;
		}

		MegapoolValidatorIndexEntry indexEntry =
			context.ValidatorInfo.Data.MegapoolValidatorIndex[(updatedEvent.MegapoolAddress, updatedEvent.ValidatorId)];

		Dictionary<(string Address, int Index), Validator> megapoolValidators =
			context.ValidatorInfo.Partial.UpdatedMegapoolValidators;

		if (!megapoolValidators.ContainsKey((updatedEvent.MegapoolAddress, updatedEvent.ValidatorId)))
		{
			megapoolValidators[(updatedEvent.MegapoolAddress, updatedEvent.ValidatorId)] =
				(await context.Storage.ReadAsync<Validator>(
					Keys.MegapoolValidator(updatedEvent.MegapoolAddress, updatedEvent.ValidatorId), cancellationToken))
				?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		megapoolValidators[(updatedEvent.MegapoolAddress, updatedEvent.ValidatorId)] = megapoolValidators[
				(updatedEvent.MegapoolAddress, updatedEvent.ValidatorId)] with
			{
				Status = updatedEvent.Status,
				History =
				[
					..megapoolValidators[(updatedEvent.MegapoolAddress, updatedEvent.ValidatorId)].History,
					new ValidatorHistory
					{
						Status = updatedEvent.Status,
						Timestamp = updatedEvent.Time,
					},
				],
			};

		return indexEntry.NodeAddress.ToHex(true);
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
		catch
		{
			// Not implemented, cannot rely on version query
			return null;
		}

		return nodeOperatorAddress;
	}

	private static async Task<string?> TryGetNodeOperator(
		NodesSyncContext context, string megapoolAddress, FilterLog filterLog,
		CancellationToken cancellationToken)
	{
		RocketMegapoolDelegateService megapoolDelegate = new(context.Web3, megapoolAddress);

		string? nodeOperatorAddress = await FetchNodeOperatorAddressFromMegapoolAddress(
			context, filterLog.BlockNumber, megapoolAddress, megapoolDelegate, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return null;
		}

		return nodeOperatorAddress;
	}
}