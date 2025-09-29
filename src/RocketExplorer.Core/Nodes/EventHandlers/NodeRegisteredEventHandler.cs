using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public static class NodeRegisteredEventHandler
{
	public static async Task HandleAsync(
		this NodesSyncContext context, EventLog<NodeRegisteredEventDTO> eventLog, CancellationToken cancellationToken)
	{
		NodeRegisteredEventDTO @event = eventLog.Event;

		context.Logger.LogInformation("Node registered {Address}", @event.Node);

		context.DashboardInfo.NodeOperators++;

		context.Nodes.Data.Index.Add(
			@event.Node, new NodeIndexEntry
			{
				ContractAddress = @event.Node.HexToByteArray(),
				RegistrationTimestamp = (long)@event.Time,
			});

		await context.GlobalIndexService.AddOrUpdateEntryAsync(
			@event.Node.HexToByteArray(), @event.Node.RemoveHexPrefix(),
			x => x.Type |= IndexEntryType.NodeOperator, cancellationToken);

		// TODO: Removed, replace
		// Fetch latest node details (otherwise we would have to use the current latestRocketNodeManager of log.BlockNumber via contractsSnapshot)
		////GetNodeDetailsOutputDTO? nodeDetails = null;

		////try
		////{
		////	// TODO: Obsolete, replace
		////	// See https://discord.com/channels/405159462932971535/704214664829075506/1365495113383677973
		////	nodeDetails =
		////		await context.Policy.ExecuteAsync(() =>
		////			context.RocketNodeManager.GetNodeDetailsQueryAsync(@event.Node));
		////}
		////catch
		////{
		////	context.Logger.LogWarning("Failed to fetch node details for {Node}", @event.Node);
		////}

		// TODO: Add more details
		context.Nodes.Partial.Updated.Add(
			@event.Node, new Node
			{
				ContractAddress = @event.Node.HexToByteArray(),
				RegistrationTimestamp = (long)@event.Time,
				Timezone = "Unknown",
			});

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)@event.Time).DateTime);
		context.Nodes.Data.DailyRegistrations[key] = context.Nodes.Data.DailyRegistrations.GetValueOrDefault(key) + 1;
		context.Nodes.Data.TotalNodesCount[key] = context.Nodes.Data.TotalNodesCount.GetLatestValueOrDefault() + 1;
	}
}