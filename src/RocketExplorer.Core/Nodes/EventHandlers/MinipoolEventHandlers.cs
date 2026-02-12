using System.Globalization;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketMinipoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolQueue.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class MinipoolEventHandlers
{
	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MinipoolEnqueuedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		MinipoolUpdatedEvent updatedEvent = new()
		{
			Log = eventLog.Log,
			Time = eventLog.Event.Time,
			MinipoolAddress = eventLog.Event.Minipool,
			Status = ValidatorStatus.InQueue,
		};

		(string? nodeOperatorAddress, ValidatorMasterInfo? validator) =
			await EventMinipoolValidatorUpdateAsync(globalContext, updatedEvent, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress) || validator is null)
		{
			return;
		}

		globalContext.DashboardContext.QueueLength++;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		MinipoolValidatorQueueEntry queueEntry = new()
		{
			NodeAddress = context.Nodes.Data.Nodes[nodeOperatorAddress].ContractAddress,
			MinipoolAddress = validator.MinipoolAddress!,
			PubKey = validator.PubKey,
			EnqueueTimestamp = (long)eventLog.Event.Time,
		};

		if ("minipools.available.half".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
			context.QueueInfo.MinipoolHalfQueue.Add(queueEntry);
		}

		if ("minipools.available.full".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
			context.QueueInfo.MinipoolFullQueue.Add(queueEntry);
		}

		if ("minipools.available.variable".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
			context.QueueInfo.MinipoolVariableQueue.Add(queueEntry);
		}

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestValueOrDefault() + 1;
		context.QueueInfo.DailyEnqueued[key] = context.QueueInfo.DailyEnqueued.GetValueOrDefault(key) + 1;
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MinipoolDequeuedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		string minipoolAddress = eventLog.Event.Minipool;

		MinipoolUpdatedEvent updatedEvent = new()
		{
			Log = eventLog.Log,
			Time = eventLog.Event.Time,
			MinipoolAddress = minipoolAddress,
			Status = ValidatorStatus.Dequeued,
		};

		(string? nodeOperatorAddress, _) =
			await EventMinipoolValidatorUpdateAsync(globalContext, updatedEvent, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		globalContext.DashboardContext.QueueLength--;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if ("minipools.available.half".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
			if (!context.QueueInfo.MinipoolHalfQueue[0].MinipoolAddress
					.SequenceEqual(updatedEvent.MinipoolAddress.HexToByteArray()))
			{
				throw new InvalidOperationException("Unexpected minipool address");
			}

			context.QueueInfo.MinipoolHalfQueue.RemoveAt(0);
		}

		if ("minipools.available.full".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
			if (!context.QueueInfo.MinipoolFullQueue[0].MinipoolAddress
					.SequenceEqual(updatedEvent.MinipoolAddress.HexToByteArray()))
			{
				throw new InvalidOperationException("Unexpected minipool address");
			}

			context.QueueInfo.MinipoolFullQueue.RemoveAt(0);
		}

		if ("minipools.available.variable".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
			if (!context.QueueInfo.MinipoolVariableQueue[0].MinipoolAddress
					.SequenceEqual(updatedEvent.MinipoolAddress.HexToByteArray()))
			{
				throw new InvalidOperationException("Unexpected minipool address");
			}

			context.QueueInfo.MinipoolVariableQueue.RemoveAt(0);
		}

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);
		context.QueueInfo.DailyDequeued[key] = context.QueueInfo.DailyDequeued.GetValueOrDefault(key) + 1;
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestValueOrDefault() - 1;
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<MinipoolPrestakedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		string minipoolAddress = eventLog.Log.Address;

		(string? nodeOperatorAddress, _) = await EventMinipoolValidatorUpdateAsync(
			globalContext, new MinipoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = eventLog.Event.Time,
				MinipoolAddress = minipoolAddress,
				Status = ValidatorStatus.PreStaked,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;
		NodeMasterInfo node = context.Nodes.Data.Nodes[nodeOperatorAddress];

		if (node.MinipoolValidators.TryGetValue(minipoolAddress, out ValidatorMasterInfo? minipoolValidator))
		{
			minipoolValidator.PubKey = eventLog.Event.ValidatorPubkey;
		}

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			eventLog.Event.ValidatorPubkey.ToHex(), minipoolAddress.HexToByteArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.MinipoolValidator;
				x.Address = minipoolAddress.HexToByteArray();
				x.ValidatorPubKey = eventLog.Event.ValidatorPubkey;
			}, cancellationToken: cancellationToken);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<EtherWithdrawalProcessedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string minipoolAddress = eventLog.Log.Address;

		await EventMinipoolValidatorUpdateAsync(
			globalContext, new MinipoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = eventLog.Event.Time,
				MinipoolAddress = minipoolAddress,
				Status = ValidatorStatus.Exited,
			}, cancellationToken);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<StatusUpdatedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string minipoolAddress = eventLog.Log.Address;

		ValidatorStatus validatorStatus = eventLog.Event.Status.ToValidatorStatus();

		(string? nodeOperatorAddress, _) = await EventMinipoolValidatorUpdateAsync(
			globalContext, new MinipoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = eventLog.Event.Time,
				MinipoolAddress = minipoolAddress,
				Status = eventLog.Event.Status.ToValidatorStatus(),
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		if (validatorStatus == ValidatorStatus.Staking)
		{
			NodesMasterContext context = await globalContext.NodesMasterContextFactory;
			NodeMasterInfo node = context.Nodes.Data.Nodes[nodeOperatorAddress];

			node.MinipoolValidators.TryGetValue(minipoolAddress, out ValidatorMasterInfo? validator);

			try
			{
				long validatorIndex = await globalContext.Services.BeaconChainService.GetValidatorIndex(
						validator?.PubKey!) ??
					throw new InvalidOperationException();

				if (validator is not null)
				{
					validator.ValidatorIndex = validatorIndex;
				}

				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					validatorIndex.ToString(CultureInfo.InvariantCulture), minipoolAddress.HexToByteArray(),
					new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
					x =>
					{
						x.Type |= IndexEntryType.MinipoolValidator;
						x.Address = minipoolAddress.HexToByteArray();
						x.ValidatorIndex = validatorIndex;
					}, cancellationToken: cancellationToken);
			}
			catch
			{
				globalContext.GetLogger<NodesSync>().LogDebug(
					"Couldn't query validator index for {Address}", minipoolAddress);
			}

			globalContext.DashboardContext.MinipoolValidatorsStaking++;
		}

		if (validatorStatus == ValidatorStatus.Exited)
		{
			globalContext.DashboardContext.MinipoolValidatorsStaking--;
		}
	}

	private static async Task<(string? NodeOperatorAddress, ValidatorMasterInfo? Validator)> EventMinipoolValidatorUpdateAsync(
		GlobalContext globalContext, MinipoolUpdatedEvent updatedEvent,
		CancellationToken cancellationToken = default)
	{
		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.MinipoolNodeAddresses.TryGetValue(updatedEvent.MinipoolAddress, out string? nodeOperatorAddress)
			|| !context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			return (null, null);
		}

		if (!node.MinipoolValidators.TryGetValue(updatedEvent.MinipoolAddress, out ValidatorMasterInfo? validator))
		{
			return (null, null);
		}

		validator.Status = updatedEvent.Status;
		validator.History.Add(new ValidatorHistory
		{
			Status = updatedEvent.Status,
			Timestamp = (long)updatedEvent.Time,
		});

		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);
		context.Nodes.MinipoolValidatorsUpdated.Add((nodeOperatorAddress, updatedEvent.MinipoolAddress));

		return (nodeOperatorAddress, validator);
	}
}
