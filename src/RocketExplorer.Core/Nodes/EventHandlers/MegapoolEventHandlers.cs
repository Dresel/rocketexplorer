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

		(string? nodeOperatorAddress, ValidatorMasterInfo? validator) = await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.PreLaunch,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress) || validator is null)
		{
			return;
		}

		globalContext.DashboardContext.QueueLength--;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		int h = 0;

		h += context.QueueInfo.MegapoolStandardQueue.RemoveAll(x =>
			x.PubKey.SequenceEqual(validator.PubKey ?? []));
		h += context.QueueInfo.MegapoolExpressQueue.RemoveAll(x =>
			x.PubKey.SequenceEqual(validator.PubKey ?? []));

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

		(string? nodeOperatorAddress, _) = await EventMegapoolValidatorUpdateAsync(
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

		RocketMegapoolDelegateService megapoolDelegate = new(globalContext.Services.Web3, megapoolAddress);

		GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(
			(uint)validatorId, new BlockParameter(eventLog.Log.BlockNumber));

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;
		NodeMasterInfo node = context.Nodes.Data.Nodes[nodeOperatorAddress];

		long validatorIndex = (long)validatorInfo.ReturnValue1.ValidatorIndex;

		if (node.MegapoolValidators.TryGetValue((megapoolAddress, validatorId), out ValidatorMasterInfo? megapoolValidator))
		{
			megapoolValidator.ValidatorIndex = validatorIndex;
		}

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			validatorInfo.ReturnValue1.ValidatorIndex.ToString(CultureInfo.InvariantCulture),
			megapoolAddress.HexToByteArray().Concat(BitConverter.GetBytes(validatorId)).ToArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.MegapoolValidator;
				x.Address = node.ContractAddress;
				x.MegapoolAddress = megapoolAddress.HexToByteArray();
				x.ValidatorIndex = validatorIndex;
				x.MegapoolIndex = validatorId;
			}, cancellationToken: cancellationToken);

		globalContext.DashboardContext.MegapoolValidatorsStaking++;
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MegapoolValidatorDissolvedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Dissolved,
			}, cancellationToken);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MegapoolValidatorExitingEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		(string? nodeOperatorAddress, _) = await EventMegapoolValidatorUpdateAsync(
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

		await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Exited,
			}, cancellationToken);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MegapoolValidatorDequeuedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string megapoolAddress = eventLog.Log.Address;
		int validatorId = (int)eventLog.Event.ValidatorId;
		long time = (long)eventLog.Event.Time;

		(string? nodeOperatorAddress, ValidatorMasterInfo? validator) = await EventMegapoolValidatorUpdateAsync(
			globalContext, new MegapoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = time,
				MegapoolAddress = megapoolAddress,
				ValidatorId = validatorId,
				Status = ValidatorStatus.Dequeued,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress) || validator is null)
		{
			return;
		}

		globalContext.DashboardContext.QueueLength--;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		int h = 0;

		h += context.QueueInfo.MegapoolStandardQueue.RemoveAll(x =>
			x.PubKey.SequenceEqual(validator.PubKey ?? []));
		h += context.QueueInfo.MegapoolExpressQueue.RemoveAll(x =>
			x.PubKey.SequenceEqual(validator.PubKey ?? []));

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

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.MegapoolNodeAddresses.TryGetValue(megapoolAddress, out string? nodeOperatorAddress))
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

		byte[] pubKey = await megapoolDelegate.GetValidatorPubkeyQueryAsync(
			(uint)eventValidatorId, new BlockParameter(eventLog.Log.BlockNumber));

		Debug.Assert(pubKey.Length > 0, "PubKey should not be empty");

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

		GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(
			(uint)eventValidatorId, new BlockParameter(eventLog.Log.BlockNumber));

		ValidatorMasterInfo validator = new()
		{
			MegapoolAddress = megapoolAddress.HexToByteArray(),
			MegapoolIndex = eventValidatorId,
			PubKey = pubKey,
			ValidatorIndex = null,
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

		NodeMasterInfo node = context.Nodes.Data.Nodes[nodeOperatorAddress];
		node.MegapoolAddress = megapoolAddress.HexToByteArray();
		node.MegapoolValidators[(megapoolAddress, eventValidatorId)] = validator;
		context.Nodes.Data.MegapoolNodeAddresses[megapoolAddress] = nodeOperatorAddress;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);
		context.Nodes.MegapoolValidatorsUpdated.Add((nodeOperatorAddress, megapoolAddress, eventValidatorId));

		context.QueueInfo.MegapoolQueueIndex++;

		MegapoolValidatorQueueEntry queueEntry = new()
		{
			NodeAddress = nodeOperatorAddress.HexToByteArray(),
			MegapoolAddress = megapoolAddress.HexToByteArray(),
			MegapoolIndex = eventValidatorId,
			PubKey = pubKey,
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

	private static async Task<(string? NodeOperatorAddress, ValidatorMasterInfo? Validator)> EventMegapoolValidatorUpdateAsync(
		GlobalContext globalContext, MegapoolUpdatedEvent updatedEvent, CancellationToken cancellationToken)
	{
		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.MegapoolNodeAddresses.TryGetValue(updatedEvent.MegapoolAddress, out string? nodeOperatorAddress)
			|| !context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			return (null, null);
		}

		if (!node.MegapoolValidators.TryGetValue((updatedEvent.MegapoolAddress, updatedEvent.ValidatorId), out ValidatorMasterInfo? validator))
		{
			return (null, null);
		}

		validator.Status = updatedEvent.Status;
		validator.History.Add(new ValidatorHistory
		{
			Status = updatedEvent.Status,
			Timestamp = updatedEvent.Time,
		});

		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);
		context.Nodes.MegapoolValidatorsUpdated.Add((nodeOperatorAddress, updatedEvent.MegapoolAddress, updatedEvent.ValidatorId));

		return (nodeOperatorAddress, validator);
	}

	private static async Task<string?> FetchNodeOperatorAddressFromMegapoolAddress(
		GlobalContext globalContext, HexBigInteger blockNumber, string megapoolAddress,
		RocketMegapoolDelegateService megapoolDelegate, CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress =
			await megapoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(blockNumber));

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.Nodes.ContainsKey(nodeOperatorAddress))
		{
			globalContext.GetLogger<MegapoolEventHandlers>().LogDebug(
				"Node operator {NodeOperatorAddress} for {MegapoolAddress} not found in index.", nodeOperatorAddress,
				megapoolAddress);
			return null;
		}

		try
		{
			if (!string.Equals(
					await globalContext.Services.RocketNodeManager.GetMegapoolAddressQueryAsync(nodeOperatorAddress),
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
