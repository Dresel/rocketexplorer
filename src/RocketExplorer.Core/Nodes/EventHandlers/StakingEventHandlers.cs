using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class StakingEventHandlers
{
	public static async Task HandleRPLLegacyStakedAsync(
		GlobalContext globalContext, EventLog<RPLLegacyStakedEventDto> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.From;

		if (!await EnsureNodeOperatorLoadedAsync(globalContext, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		NodesContext context = await globalContext.NodesContextFactory;

		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLLegacyStaked += eventLog.Event.Amount;
		globalContext.DashboardContext.RPLLegacyStakedTotal += eventLog.Event.Amount;
	}

	public static async Task HandleRPLLegacyUnstakedAsync(
		GlobalContext globalContext, EventLog<RPLLegacyWithdrawnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.To;

		if (!await EnsureNodeOperatorLoadedAsync(globalContext, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		NodesContext context = await globalContext.NodesContextFactory;
		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLLegacyStaked += eventLog.Event.Amount;
		globalContext.DashboardContext.RPLLegacyStakedTotal -= eventLog.Event.Amount;
	}

	public static async Task HandleRPLLegacyUnstakedAsync(
		GlobalContext globalContext, EventLog<RPLOrRPLLegacyWithdrawnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.To;

		if (!await EnsureNodeOperatorLoadedAsync(globalContext, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		NodesContext context = await globalContext.NodesContextFactory;
		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLLegacyStaked -= eventLog.Event.Amount;
		globalContext.DashboardContext.RPLLegacyStakedTotal -= eventLog.Event.Amount;
	}

	public static async Task HandleRPLMegapoolStakedAsync(
		GlobalContext globalContext, EventLog<RPLStakedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.Node;

		if (!await EnsureNodeOperatorLoadedAsync(globalContext, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		NodesContext context = await globalContext.NodesContextFactory;
		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLMegapoolStaked += eventLog.Event.Amount;
		globalContext.DashboardContext.RPLMegapoolStakedTotal += eventLog.Event.Amount;
	}

	public static async Task HandleRPLMegapoolUnstakedAsync(
		GlobalContext globalContext, EventLog<RPLUnstakedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.From;

		if (!await EnsureNodeOperatorLoadedAsync(globalContext, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		NodesContext context = await globalContext.NodesContextFactory;
		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLMegapoolStaked -= eventLog.Event.Amount;
		globalContext.DashboardContext.RPLMegapoolStakedTotal -= eventLog.Event.Amount;
	}

	private static async Task<bool> EnsureNodeOperatorLoadedAsync(
		GlobalContext globalContext, CancellationToken cancellationToken,
		string nodeOperatorAddress)
	{
		NodesContext context = await globalContext.NodesContextFactory;

		// This should not happen
		if (!context.Nodes.Data.Index.ContainsKey(nodeOperatorAddress))
		{
			globalContext.GetLogger<StakingEventHandlers>().LogError("Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return false;
		}

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await globalContext.Services.Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		return true;
	}
}