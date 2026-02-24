using System.Diagnostics;
using System.Globalization;
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
		GlobalContext globalContext, EventLog<MegapoolValidatorAssignedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
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

		globalContext.DashboardContext.QueueLength--;

		int h = 0;

		NodesContext context = await globalContext.NodesContextFactory;

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
		GlobalContext globalContext, EventLog<MegapoolValidatorStakedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
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

		NodesContext context = await globalContext.NodesContextFactory;

		try
		{
			long validatorIndex = await globalContext.Services.BeaconChainService.GetValidatorIndex(
					context.ValidatorInfo.Data.MegapoolValidatorIndex[(megapoolAddress, validatorId)].PubKey) ??
				throw new InvalidOperationException();

			context.ValidatorInfo.Data.MegapoolValidatorIndex[(megapoolAddress, validatorId)] =
				context.ValidatorInfo.Data.MegapoolValidatorIndex[(megapoolAddress, validatorId)] with
				{
					ValidatorIndex = validatorIndex,
				};

			context.ValidatorInfo.Partial.UpdatedMegapoolValidators[(megapoolAddress, validatorId)] =
				context.ValidatorInfo.Partial.UpdatedMegapoolValidators[(megapoolAddress, validatorId)] with
				{
					ValidatorIndex = validatorIndex,
				};

			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				validatorIndex.ToString(CultureInfo.InvariantCulture),
				megapoolAddress.HexToByteArray().Concat(BitConverter.GetBytes(validatorId)).ToArray(),
				new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
				x =>
				{
					x.Type |= IndexEntryType.MegapoolValidator;
					x.Address = context.ValidatorInfo.Data.MegapoolValidatorIndex[(megapoolAddress, validatorId)]
						.NodeAddress;
					x.MegapoolAddress = megapoolAddress.HexToByteArray();
					x.ValidatorIndex = validatorIndex;
					x.MegapoolIndex = validatorId;
				}, cancellationToken: cancellationToken);
		}
		catch
		{
			globalContext.GetLogger<MegapoolEventHandlers>().LogDebug(
				"Couldn't query validator index for {Address}", megapoolAddress);
		}

		globalContext.DashboardContext.MegapoolValidatorsStaking++;
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MegapoolValidatorDissolvedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Dissolved,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
		}
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MegapoolValidatorExitingEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
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

		globalContext.DashboardContext.MegapoolValidatorsStaking--;
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MegapoolValidatorExitedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
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
		GlobalContext globalContext, EventLog<MegapoolValidatorDequeuedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		string? nodeOperatorAddress = await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
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

		globalContext.DashboardContext.QueueLength--;

		NodesContext context = await globalContext.NodesContextFactory;

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
		GlobalContext globalContext, EventLog<MegapoolValidatorEnqueuedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string megapoolAddress = eventLog.Log.Address;
		int eventValidatorId = (int)eventLog.Event.ValidatorId;

		NodesContext context = await globalContext.NodesContextFactory;

		KeyValuePair<string, NodeIndexEntry>? indexEntry = context.Nodes.Data.Index
			.Where(node => node.Value.MegapoolAddress?.SequenceEqual(eventLog.Log.Address.HexToByteArray()) == true)
			.Cast<KeyValuePair<string, NodeIndexEntry>?>().SingleOrDefault();
		string? nodeOperatorAddress = indexEntry?.Value.ContractAddress.ToHex(true);

		if (indexEntry == null)
		{
			nodeOperatorAddress = await TryGetNodeOperator(
				globalContext, megapoolAddress, eventLog.Log, cancellationToken);
		}

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		globalContext.DashboardContext.QueueLength++;

		RocketMegapoolDelegateService megapoolDelegate = new(globalContext.Services.Web3, megapoolAddress);

		byte[] pubKey = await globalContext.Policy.ExecuteAsync(() => megapoolDelegate.GetValidatorPubkeyQueryAsync(
			(uint)eventValidatorId, new BlockParameter(eventLog.Log.BlockNumber)));

		Debug.Assert(pubKey.Length > 0, "PubKey should not be empty");

		MegapoolValidatorIndexEntry entry = new()
		{
			NodeAddress = nodeOperatorAddress.HexToByteArray(),
			MegapoolAddress = megapoolAddress.HexToByteArray(),
			MegapoolIndex = eventValidatorId,
			PubKey = pubKey,
			ValidatorIndex = null,
		};

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			megapoolAddress.RemoveHexPrefix(), megapoolAddress.HexToByteArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.Megapool;
				x.Address = nodeOperatorAddress.HexToByteArray();
				x.MegapoolAddress = megapoolAddress.HexToByteArray();
			}, cancellationToken: cancellationToken);

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			pubKey.ToHex(), megapoolAddress.HexToByteArray().Concat(BitConverter.GetBytes(eventValidatorId)).ToArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.MegapoolValidator;
				x.Address = nodeOperatorAddress.HexToByteArray();
				x.MegapoolAddress = megapoolAddress.HexToByteArray();
				x.ValidatorPubKey = pubKey;
				x.MegapoolIndex = eventValidatorId;
			}, cancellationToken: cancellationToken);

		context.ValidatorInfo.Data.MegapoolValidatorIndex.Add(
			(megapoolAddress, (int)eventLog.Event.ValidatorId), entry);

		context.Nodes.Data.Index[nodeOperatorAddress] = context.Nodes.Data.Index[nodeOperatorAddress] with
		{
			MegapoolAddress = megapoolAddress.HexToByteArray(),
		};

		GetValidatorInfoOutputDTO validatorInfo = await globalContext.Policy.ExecuteAsync(() => megapoolDelegate.GetValidatorInfoQueryAsync(
			(uint)eventValidatorId, new BlockParameter(eventLog.Log.BlockNumber)));

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
				(await globalContext.Services.Storage.ReadAsync<Node>(
					Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
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
		GlobalContext globalContext, MegapoolUpdatedEvent updatedEvent, CancellationToken cancellationToken)
	{
		NodesContext context = await globalContext.NodesContextFactory;

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
				(await globalContext.Services.Storage.ReadAsync<Validator>(
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
		GlobalContext globalContext, HexBigInteger blockNumber, string megapoolAddress,
		RocketMegapoolDelegateService megapoolDelegate, CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress =
			await globalContext.Policy.ExecuteAsync(() => megapoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(blockNumber)));

		NodesContext context = await globalContext.NodesContextFactory;

		// If not found might be megapool from different rocket pool version
		if (!context.Nodes.Data.Index.ContainsKey(nodeOperatorAddress))
		{
			globalContext.GetLogger<MegapoolEventHandlers>().LogDebug(
				"Node operator {NodeOperatorAddress} for {MegapoolAddress} not found in index.", nodeOperatorAddress,
				megapoolAddress);
			return null;
		}

		try
		{
			// Can happen if the same node operator address is used for multiple rocket pool deployments
			if (!string.Equals(
					await globalContext.Policy.ExecuteAsync(() => globalContext.Services.RocketNodeManager.GetMegapoolAddressQueryAsync(nodeOperatorAddress)),
					megapoolAddress,
					StringComparison.OrdinalIgnoreCase))
			{
				globalContext.GetLogger<MegapoolEventHandlers>().LogDebug(
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
		GlobalContext context, string megapoolAddress, FilterLog filterLog,
		CancellationToken cancellationToken)
	{
		RocketMegapoolDelegateService megapoolDelegate = new(context.Services.Web3, megapoolAddress);

		string? nodeOperatorAddress = await FetchNodeOperatorAddressFromMegapoolAddress(
			context, filterLog.BlockNumber, megapoolAddress, megapoolDelegate, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return null;
		}

		return nodeOperatorAddress;
	}
}