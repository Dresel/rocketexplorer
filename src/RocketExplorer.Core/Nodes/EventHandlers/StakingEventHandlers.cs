using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class StakingEventHandlers
{
	public static async Task HandleRPLLegacyStakedAsync(
		NodesSyncContext context, EventLog<RPLLegacyStakedEventDto> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.From;

		if (!await EnsureNodeOperatorLoadedAsync(context, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLLegacyStaked += eventLog.Event.Amount;
		context.DashboardInfo.RPLLegacyStakedTotal += eventLog.Event.Amount;
	}

	public static async Task HandleRPLLegacyUnstakedAsync(
		NodesSyncContext context, EventLog<RPLLegacyWithdrawnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.To;

		if (!await EnsureNodeOperatorLoadedAsync(context, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLLegacyStaked -= eventLog.Event.Amount;
		context.DashboardInfo.RPLLegacyStakedTotal -= eventLog.Event.Amount;
	}

	public static async Task HandleRPLLegacyUnstakedAsync(
		NodesSyncContext context, EventLog<RPLOrRPLLegacyWithdrawnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.To;

		if (!await EnsureNodeOperatorLoadedAsync(context, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLLegacyStaked -= eventLog.Event.Amount;
		context.DashboardInfo.RPLLegacyStakedTotal -= eventLog.Event.Amount;
	}

	public static async Task HandleRPLMegapoolStakedAsync(
		NodesSyncContext context, EventLog<RPLStakedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.Node;

		if (!await EnsureNodeOperatorLoadedAsync(context, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLMegapoolStaked += eventLog.Event.Amount;
		context.DashboardInfo.RPLMegapoolStakedTotal += eventLog.Event.Amount;
	}

	public static async Task HandleRPLMegapoolUnstakedAsync(
		NodesSyncContext context, EventLog<RPLUnstakedEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress = eventLog.Event.From;

		if (!await EnsureNodeOperatorLoadedAsync(context, cancellationToken, nodeOperatorAddress))
		{
			return;
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress].RPLMegapoolStaked -= eventLog.Event.Amount;
		context.DashboardInfo.RPLMegapoolStakedTotal -= eventLog.Event.Amount;
	}

	private static async Task<bool> EnsureNodeOperatorLoadedAsync(
		NodesSyncContext context, CancellationToken cancellationToken,
		string nodeOperatorAddress)
	{
		// This should not happen
		if (!context.Nodes.Data.Index.ContainsKey(nodeOperatorAddress))
		{
			context.Logger.LogError("Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return false;
		}

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await context.Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		return true;
	}
}