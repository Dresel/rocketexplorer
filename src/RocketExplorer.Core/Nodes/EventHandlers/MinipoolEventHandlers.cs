using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketMinipoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolQueue.ContractDefinition;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class MinipoolEventHandlers
{
	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MinipoolEnqueuedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		MinipoolUpdatedEvent updatedEvent = new()
		{
			Log = eventLog.Log,
			Time = eventLog.Event.Time,
			MinipoolAddress = eventLog.Event.Minipool,
			Status = ValidatorStatus.InQueue,
		};

		string? nodeOperatorAddress = await EventMinipoolValidatorUpdateAsync(context, updatedEvent, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		context.DashboardInfo.QueueLength++;

		MinipoolValidatorIndexEntry indexEntry = context.ValidatorInfo.Data.MinipoolValidatorIndex[updatedEvent.MinipoolAddress];
		MinipoolValidatorQueueEntry queueEntry = new()
		{
			NodeAddress = indexEntry.NodeAddress,
			MinipoolAddress = indexEntry.MinipoolAddress,
			PubKey = indexEntry.PubKey,
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

		// TODO: Throw if none
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestValueOrDefault() + 1;
		context.QueueInfo.DailyEnqueued[key] = context.QueueInfo.DailyEnqueued.GetValueOrDefault(key) + 1;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MinipoolDequeuedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		MinipoolUpdatedEvent updatedEvent = new()
		{
			Log = eventLog.Log,
			Time = eventLog.Event.Time,
			MinipoolAddress = eventLog.Event.Minipool,
			Status = ValidatorStatus.Dequeued,
		};

		string? nodeOperatorAddress = await EventMinipoolValidatorUpdateAsync(context, updatedEvent, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		context.DashboardInfo.QueueLength--;

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
		NodesSyncContext context, EventLog<MinipoolPrestakedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		string minipoolAddress = eventLog.Log.Address;

		string? nodeOperatorAddress = await EventMinipoolValidatorUpdateAsync(
			context, new MinipoolUpdatedEvent
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

		context.ValidatorInfo.Data.MinipoolValidatorIndex[minipoolAddress] =
			context.ValidatorInfo.Data.MinipoolValidatorIndex[minipoolAddress] with
			{
				PubKey = eventLog.Event.ValidatorPubkey,
			};

		context.ValidatorInfo.Partial.UpdatedMinipoolValidators[minipoolAddress] =
			context.ValidatorInfo.Partial.UpdatedMinipoolValidators[minipoolAddress] with
			{
				PubKey = eventLog.Event.ValidatorPubkey,
			};

		context.Nodes.Partial.Updated[nodeOperatorAddress].MinipoolValidators.ReplaceWhere(
			x => x.MinipoolAddress.SequenceEqual(minipoolAddress.HexToByteArray()),
			x => x with
			{
				PubKey = eventLog.Event.ValidatorPubkey,
			});
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<EtherWithdrawalProcessedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string minipoolAddress = eventLog.Log.Address;

		string? nodeOperatorAddress = await EventMinipoolValidatorUpdateAsync(
			context, new MinipoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = eventLog.Event.Time,
				MinipoolAddress = minipoolAddress,
				Status = ValidatorStatus.Exited,
			}, cancellationToken);
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<StatusUpdatedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		string minipoolAddress = eventLog.Log.Address;

		ValidatorStatus validatorStatus = eventLog.Event.Status.ToValidatorStatus();

		string? nodeOperatorAddress = await EventMinipoolValidatorUpdateAsync(
			context, new MinipoolUpdatedEvent
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
			context.DashboardInfo.MinipoolValidatorsStaking++;
		}

		if (validatorStatus == ValidatorStatus.Exited)
		{
			context.DashboardInfo.MinipoolValidatorsStaking--;
		}
	}

	private static async Task<string?> EventMinipoolValidatorUpdateAsync(
		NodesSyncContext context, MinipoolUpdatedEvent updatedEvent,
		CancellationToken cancellationToken = default)
	{
		if (!context.ValidatorInfo.Data.MinipoolValidatorIndex.TryGetValue(
				updatedEvent.MinipoolAddress, out MinipoolValidatorIndexEntry? indexEntry))
		{
			return null;
		}

		// TODO: Can get from index?
		string nodeOperatorAddress = indexEntry.NodeAddress.ToHex(true);

		if (!context.ValidatorInfo.Partial.UpdatedMinipoolValidators.ContainsKey(updatedEvent.MinipoolAddress))
		{
			context.ValidatorInfo.Partial.UpdatedMinipoolValidators[updatedEvent.MinipoolAddress] =
				(await context.Storage.ReadAsync<Validator>(
					Keys.MinipoolValidator(updatedEvent.MinipoolAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.ValidatorInfo.Partial.UpdatedMinipoolValidators[updatedEvent.MinipoolAddress] =
			context.ValidatorInfo.Partial.UpdatedMinipoolValidators[updatedEvent.MinipoolAddress] with
			{
				Status = updatedEvent.Status,
				History =
				[
					.. context.ValidatorInfo.Partial.UpdatedMinipoolValidators[updatedEvent.MinipoolAddress].History,
					new ValidatorHistory
					{
						Status = updatedEvent.Status,
						Timestamp = (long)updatedEvent.Time,
					},
				],
			};

		return nodeOperatorAddress;
	}
}