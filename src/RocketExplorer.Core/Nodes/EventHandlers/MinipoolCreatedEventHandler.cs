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
		GlobalContext globalContext, EventLog<MinipoolCreatedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		MinipoolCreatedEventDTO @event = eventLog.Event;

		RocketMinipoolDelegateService minipoolDelegate = new(globalContext.Services.Web3, @event.Minipool);
		string nodeOperatorAddress =
			await minipoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(eventLog.Log.BlockNumber));

		NodesContext context = await globalContext.NodesContextFactory;

		// This should not happen
		if (!context.Nodes.Data.Index.TryGetValue(nodeOperatorAddress, out NodeIndexEntry? nodeIndexEntry))
		{
			globalContext.GetLogger<MinipoolCreatedEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} for {Minipool} not found in index.", nodeOperatorAddress,
				@event.Minipool);
			return;
		}

		MinipoolValidatorIndexEntry entry = new()
		{
			NodeAddress = nodeIndexEntry.ContractAddress,
			MinipoolAddress = @event.Minipool.HexToByteArray(),
			PubKey = null,
			ValidatorIndex = null,
		};

		context.ValidatorInfo.Data.MinipoolValidatorIndex.Add(@event.Minipool, entry);

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			@event.Minipool.RemoveHexPrefix(), @event.Minipool.HexToByteArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.MinipoolValidator;
				x.Address = @event.Minipool.HexToByteArray();
			}, cancellationToken: cancellationToken);

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
				(await globalContext.Services.Storage.ReadAsync<Node>(
					Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress] = context.Nodes.Partial.Updated[nodeOperatorAddress] with
		{
			MinipoolValidators =
			[
				..context.Nodes.Partial.Updated[nodeOperatorAddress].MinipoolValidators, entry,
			],
		};
	}
}