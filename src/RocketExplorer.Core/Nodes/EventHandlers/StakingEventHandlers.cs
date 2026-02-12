using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class StakingEventHandlers
{
	public static async Task HandleRPLLegacyStakedAsync(
		GlobalContext globalContext, EventLog<RPLLegacyStakedEventDto> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.From;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<StakingEventHandlers>().LogError("Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		node.RPLLegacyStaked += eventLog.Event.Amount;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

		globalContext.DashboardContext.RPLLegacyStakedTotal += eventLog.Event.Amount;
	}

	public static async Task HandleRPLLegacyUnstakedAsync(
		GlobalContext globalContext, EventLog<RPLLegacyWithdrawnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.To;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<StakingEventHandlers>().LogError("Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		node.RPLLegacyStaked -= eventLog.Event.Amount;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

		globalContext.DashboardContext.RPLLegacyStakedTotal -= eventLog.Event.Amount;
	}

	public static async Task HandleRPLLegacyUnstakedAsync(
		GlobalContext globalContext, EventLog<RPLOrRPLLegacyWithdrawnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.To;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<StakingEventHandlers>().LogError("Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		node.RPLLegacyStaked -= eventLog.Event.Amount;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

		globalContext.DashboardContext.RPLLegacyStakedTotal -= eventLog.Event.Amount;
	}

	public static async Task HandleRPLMegapoolStakedAsync(
		GlobalContext globalContext, EventLog<RPLStakedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.Node;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<StakingEventHandlers>().LogError("Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		node.RPLMegapoolStaked += eventLog.Event.Amount;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

		globalContext.DashboardContext.RPLMegapoolStakedTotal += eventLog.Event.Amount;
	}

	public static async Task HandleRPLMegapoolUnstakedAsync(
		GlobalContext globalContext, EventLog<RPLUnstakedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.From;

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<StakingEventHandlers>().LogError("Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		node.RPLMegapoolStaked -= eventLog.Event.Amount;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

		globalContext.DashboardContext.RPLMegapoolStakedTotal -= eventLog.Event.Amount;
	}
}
