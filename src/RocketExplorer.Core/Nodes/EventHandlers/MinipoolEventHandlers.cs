using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketMinipoolDelegate;
using RocketExplorer.Ethereum.RocketMinipoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolQueue.ContractDefinition;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

internal class MinipoolEventHandlers
{
	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MinipoolEnqueuedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		string? nodeOperatorAddress = await EventMinipoolValidatorUpdateAsync(
			context, new MinipoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = eventLog.Event.Time,
				MinipoolAddress = eventLog.Event.Minipool,
				Status = ValidatorStatus.InQueue,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		// Enqueue
		if ("minipools.available.half".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
		}

		if ("minipools.available.full".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
		}

		if ("minipools.available.variable".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
		}

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() + 1;
		context.QueueInfo.DailyEnqueued[key] = context.QueueInfo.DailyEnqueued.GetValueOrDefault(key) + 1;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MinipoolDequeuedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		string? nodeOperatorAddress = await EventMinipoolValidatorUpdateAsync(
			context, new MinipoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = eventLog.Event.Time,
				MinipoolAddress = eventLog.Event.Minipool,
				Status = ValidatorStatus.Dequeued,
			}, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return;
		}

		if ("minipools.available.half".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
		}

		if ("minipools.available.full".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
		}

		if ("minipools.available.variable".Sha3().SequenceEqual(eventLog.Event.QueueId))
		{
		}

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);
		context.QueueInfo.DailyDequeued[key] = context.QueueInfo.DailyDequeued.GetValueOrDefault(key) + 1;
		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() - 1;
	}

	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MinipoolPrestakedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		string minipoolAddress = eventLog.Log.Address.ConvertToEthereumChecksumAddress();

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
		string minipoolAddress = eventLog.Log.Address.ConvertToEthereumChecksumAddress();

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
		string minipoolAddress = eventLog.Log.Address.ConvertToEthereumChecksumAddress();

		string? nodeOperatorAddress = await EventMinipoolValidatorUpdateAsync(
			context, new MinipoolUpdatedEvent
			{
				Log = eventLog.Log,
				Time = eventLog.Event.Time,
				MinipoolAddress = minipoolAddress,
				Status = eventLog.Event.Status.ToValidatorStatus(),
			}, cancellationToken);
	}

	private static async Task<string?> EventMinipoolValidatorUpdateAsync(
		NodesSyncContext context, MinipoolUpdatedEvent updatedEvent,
		CancellationToken cancellationToken = default)
	{
		if (!context.ValidatorInfo.Data.MinipoolValidatorIndex.ContainsKey(updatedEvent.MinipoolAddress))
		{
			return null;
		}

		string? nodeOperatorAddress = await TryGetNodeOperator(
			context, updatedEvent.MinipoolAddress, updatedEvent.Log, cancellationToken);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			return null;
		}

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

	private static async Task<string?> FetchNodeOperatorAddressFromMinipoolAddress(
		NodesSyncContext context, HexBigInteger blockNumber, string minipoolAddress,
		RocketMinipoolDelegateService minipoolDelegate, CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress =
			await minipoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(blockNumber));

		// If not found might be minipool from different rocket pool version
		if (!context.Nodes.Data.Index.ContainsKey(nodeOperatorAddress))
		{
			context.Logger.LogDebug(
				"Node operator {NodeOperatorAddress} for {MinipoolAddress} not found in index.", nodeOperatorAddress,
				minipoolAddress);
			return null;
		}

		// Can happen if the same node operator address is used for multiple rocket pool deployments
		if (!await context.RocketMinipoolManager.GetMinipoolExistsQueryAsync(nodeOperatorAddress))
		{
			context.Logger.LogDebug(
				"Node operator {NodeOperatorAddress} found in index but minipool address {MinipoolAddress} does not exist.",
				nodeOperatorAddress, minipoolAddress);
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
		NodesSyncContext context, string minipoolAddress, FilterLog log,
		CancellationToken cancellationToken)
	{
		RocketMinipoolDelegateService minipoolDelegate = new(context.Web3, minipoolAddress);
		context.ValidatorInfo.Cache.MinipoolNodeOperatorMap.TryGetValue(minipoolAddress, out string? nodeOperatorAddress);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			nodeOperatorAddress = await FetchNodeOperatorAddressFromMinipoolAddress(
				context, log.BlockNumber, minipoolAddress, minipoolDelegate, cancellationToken);

			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
			{
				return null;
			}

			context.ValidatorInfo.Cache.MinipoolNodeOperatorMap[minipoolAddress] = nodeOperatorAddress;
		}

		return nodeOperatorAddress;
	}
}