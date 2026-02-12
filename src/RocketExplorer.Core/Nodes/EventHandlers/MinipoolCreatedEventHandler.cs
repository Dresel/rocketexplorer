using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using RocketExplorer.Ethereum.RocketMinipoolDelegate;
using RocketExplorer.Ethereum.RocketMinipoolManager.ContractDefinition;
using RocketExplorer.Shared;
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

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<MinipoolCreatedEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} for {Minipool} not found in index.", nodeOperatorAddress,
				@event.Minipool);
			return;
		}

		ValidatorMasterInfo validator = new()
		{
			MinipoolAddress = @event.Minipool.HexToByteArray(),
			PubKey = null,
			ValidatorIndex = null,
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
		};

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			@event.Minipool.RemoveHexPrefix(), @event.Minipool.HexToByteArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.MinipoolValidator;
				x.Address = @event.Minipool.HexToByteArray();
			}, cancellationToken: cancellationToken);

		node.MinipoolValidators[@event.Minipool] = validator;
		context.Nodes.Data.MinipoolNodeAddresses[@event.Minipool] = nodeOperatorAddress;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);
		context.Nodes.MinipoolValidatorsUpdated.Add((nodeOperatorAddress, @event.Minipool));
	}
}
