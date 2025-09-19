using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using RocketExplorer.Ethereum.RocketMinipoolDelegate;
using RocketExplorer.Ethereum.RocketMinipoolManager.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class MinipoolCreatedEventHandler
{
	public static async Task HandleAsync(
		NodesSyncContext context, EventLog<MinipoolCreatedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		MinipoolCreatedEventDTO @event = eventLog.Event;

		RocketMinipoolDelegateService minipoolDelegate = new(context.Web3, @event.Minipool);
		string nodeOperatorAddress =
			await minipoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(eventLog.Log.BlockNumber));

		// This should not happen
		if (!context.Nodes.Data.Index.ContainsKey(nodeOperatorAddress))
		{
			context.Logger.LogError(
				"Node operator {NodeOperatorAddress} for {Minipool} not found in index.", nodeOperatorAddress,
				@event.Minipool);
			return;
		}

		MinipoolValidatorIndexEntry entry = new()
		{
			NodeAddress = nodeOperatorAddress.HexToByteArray(),
			MinipoolAddress = @event.Minipool.HexToByteArray(),
			PubKey = null,
			ValidatorIndex = null,
		};

		context.ValidatorInfo.Data.MinipoolValidatorIndex.Add(@event.Minipool, entry);

		context.ValidatorInfo.Partial.UpdatedMinipoolValidators.Add(
			@event.Minipool, new Validator
			{
				NodeAddress = entry.NodeAddress,
				MinipoolAddress = entry.MinipoolAddress,
				PubKey = entry.PubKey,
				ValidatorIndex = entry.ValidatorIndex,
				Status = ValidatorStatus.Created,
				Bond = (float)UnitConversion.Convert.FromWei(
					await minipoolDelegate.GetNodeDepositBalanceQueryAsync(
						new BlockParameter(eventLog.Log.BlockNumber))),
				Type = ValidatorType.Legacy,
				History =
				[
					new ValidatorHistory
					{
						Status = ValidatorStatus.Created,
						Timestamp = (long)@event.Time,
					},
				],
			});

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await context.Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress].MinipoolValidators =
		[
			..context.Nodes.Partial.Updated[nodeOperatorAddress].MinipoolValidators, entry,
		];
	}
}