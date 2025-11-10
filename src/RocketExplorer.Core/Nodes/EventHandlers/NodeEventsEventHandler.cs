using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
using RocketExplorer.Ethereum.rocketStorage.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class NodeEventsEventHandler
{
	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeWithdrawalAddressSetEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		NodesContext context = await globalContext.NodesContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		// This should not happen
		if (!context.Nodes.Data.Index.TryGetValue(nodeOperatorAddress, out NodeIndexEntry? nodeIndexEntry))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		context.Nodes.Data.WithdrawalAddresses[nodeOperatorAddress] = eventLog.Event.WithdrawalAddress;

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			eventLog.Event.WithdrawalAddress.RemoveHexPrefix(), nodeIndexEntry.ContractAddress,
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.WithdrawalAddress;
				x.Address = nodeIndexEntry.ContractAddress;
				x.WithdrawalAddress = eventLog.Event.WithdrawalAddress.HexToByteArray();
			}, cancellationToken: cancellationToken);

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await globalContext.Services.Storage.ReadAsync<Node>(
					Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress] = context.Nodes.Partial.Updated[nodeOperatorAddress] with
		{
			WithdrawalAddress = eventLog.Event.WithdrawalAddress.HexToByteArray(),
		};

		await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
			eventLog.Event.WithdrawalAddress.HexToByteArray(), cancellationToken: cancellationToken);

		globalContext.GetLogger<NodeEventsEventHandler>()
			.LogInformation(
				"Node {NodeAddress} withdrawal address set to {WithdrawalAddress}", nodeOperatorAddress,
				eventLog.Event.WithdrawalAddress);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<StakeRPLForAllowedEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		NodesContext context = await globalContext.NodesContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		// This should not happen
		if (!context.Nodes.Data.Index.TryGetValue(nodeOperatorAddress, out NodeIndexEntry? nodeIndexEntry))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await globalContext.Services.Storage.ReadAsync<Node>(
					Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		string stakeOnBehalfAddress = eventLog.Event.Caller;

		if (eventLog.Event.Allowed)
		{
			context.Nodes.Data.StakeOnBehalfAddresses[nodeOperatorAddress].Add(stakeOnBehalfAddress);

			context.Nodes.Partial.Updated[nodeOperatorAddress] = context.Nodes.Partial.Updated[nodeOperatorAddress] with
			{
				StakeOnBehalfAddresses = new HashSet<byte[]>(
					context.Nodes.Partial.Updated[nodeOperatorAddress].StakeOnBehalfAddresses
						.Union([stakeOnBehalfAddress.HexToByteArray(),]), new FastByteArrayComparer()),
			};

			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				stakeOnBehalfAddress.RemoveHexPrefix(), nodeIndexEntry.ContractAddress,
				new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
				x =>
				{
					x.Type |= IndexEntryType.StakeOnBehalfAddress;
					x.Address = nodeIndexEntry.ContractAddress;
					x.StakeOnBehalfAddresses.Add(stakeOnBehalfAddress.HexToByteArray());
				}, cancellationToken: cancellationToken);

			await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
				stakeOnBehalfAddress.HexToByteArray(), cancellationToken: cancellationToken);

			globalContext.GetLogger<NodeEventsEventHandler>()
				.LogInformation("Node {NodeAddress} stake on behalf address {StakeOnBehalfAddress} added", nodeOperatorAddress, stakeOnBehalfAddress);
		}
		else
		{
			context.Nodes.Data.StakeOnBehalfAddresses[nodeOperatorAddress].Remove(stakeOnBehalfAddress);

			context.Nodes.Partial.Updated[nodeOperatorAddress] = context.Nodes.Partial.Updated[nodeOperatorAddress] with
			{
				StakeOnBehalfAddresses = new HashSet<byte[]>(
					context.Nodes.Partial.Updated[nodeOperatorAddress].StakeOnBehalfAddresses
						.Except([stakeOnBehalfAddress.HexToByteArray(),]), new FastByteArrayComparer()),
			};

			_ = globalContext.Services.GlobalIndexService.UpdateEntryAsync(
				stakeOnBehalfAddress.RemoveHexPrefix(), nodeIndexEntry.ContractAddress,
				new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
				x =>
				{
					x.Type &= ~IndexEntryType.StakeOnBehalfAddress;
					x.Address = nodeIndexEntry.ContractAddress;
					x.StakeOnBehalfAddresses.Remove(stakeOnBehalfAddress.HexToByteArray());
				}, cancellationToken: cancellationToken);

			await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
				stakeOnBehalfAddress.HexToByteArray(), cancellationToken: cancellationToken);

			globalContext.GetLogger<NodeEventsEventHandler>()
				.LogInformation("Node {NodeAddress} stake on behalf address {StakeOnBehalfAddress} removed", nodeOperatorAddress, stakeOnBehalfAddress);
		}
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeRPLWithdrawalAddressUnsetEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		NodesContext context = await globalContext.NodesContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		// This should not happen
		if (!context.Nodes.Data.Index.TryGetValue(nodeOperatorAddress, out NodeIndexEntry? nodeIndexEntry))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		context.Nodes.Data.RPLWithdrawalAddresses.Remove(nodeOperatorAddress);

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await globalContext.Services.Storage.ReadAsync<Node>(
					Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		_ = globalContext.Services.GlobalIndexService.UpdateEntryAsync(
			context.Nodes.Partial.Updated[nodeOperatorAddress].RPLWithdrawalAddress?.ToHex() ??
			throw new InvalidOperationException("Withdrawal address should not be null"),
			nodeIndexEntry.ContractAddress,
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type &= ~IndexEntryType.RPLWithdrawalAddress;
				x.RPLWithdrawalAddress = null;
			}, entry => entry.Type == 0, cancellationToken);

		await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
			context.Nodes.Partial.Updated[nodeOperatorAddress].RPLWithdrawalAddress ??
			throw new InvalidOperationException("Withdrawal address should not be null"),
			cancellationToken: cancellationToken);

		context.Nodes.Partial.Updated[nodeOperatorAddress] = context.Nodes.Partial.Updated[nodeOperatorAddress] with
		{
			RPLWithdrawalAddress = null,
		};

		globalContext.GetLogger<NodeEventsEventHandler>()
			.LogInformation("Node {NodeAddress} RPL withdrawal address unset", nodeOperatorAddress);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeRPLWithdrawalAddressSetEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		NodesContext context = await globalContext.NodesContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		// This should not happen
		if (!context.Nodes.Data.Index.TryGetValue(nodeOperatorAddress, out NodeIndexEntry? nodeIndexEntry))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		context.Nodes.Data.RPLWithdrawalAddresses[nodeOperatorAddress] = eventLog.Event.WithdrawalAddress;

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			eventLog.Event.WithdrawalAddress.RemoveHexPrefix(), nodeIndexEntry.ContractAddress,
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.RPLWithdrawalAddress;
				x.Address = nodeIndexEntry.ContractAddress;
				x.RPLWithdrawalAddress = eventLog.Event.WithdrawalAddress.HexToByteArray();
			}, cancellationToken: cancellationToken);

		if (!context.Nodes.Partial.Updated.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes.Partial.Updated[nodeOperatorAddress] =
				(await globalContext.Services.Storage.ReadAsync<Node>(
					Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.Nodes.Partial.Updated[nodeOperatorAddress] = context.Nodes.Partial.Updated[nodeOperatorAddress] with
		{
			RPLWithdrawalAddress = eventLog.Event.WithdrawalAddress.HexToByteArray(),
		};

		await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
			eventLog.Event.WithdrawalAddress.HexToByteArray(), cancellationToken: cancellationToken);

		globalContext.GetLogger<NodeEventsEventHandler>()
			.LogInformation(
				"Node {NodeAddress} RPL withdrawal address set to {RPLWithdrawalAddress}", nodeOperatorAddress,
				eventLog.Event.WithdrawalAddress);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeRegisteredEventDTO> eventLog, CancellationToken cancellationToken)
	{
		NodeRegisteredEventDTO @event = eventLog.Event;

		globalContext.GetLogger<NodeEventsEventHandler>().LogInformation("Node registered {Address}", @event.Node);

		NodesContext context = await globalContext.NodesContextFactory;

		globalContext.DashboardContext.NodeOperators++;

		context.Nodes.Data.Index.Add(
			@event.Node, new NodeIndexEntry
			{
				ContractAddress = @event.Node.HexToByteArray(),
				RegistrationTimestamp = (long)@event.Time,
			});

		await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
			@event.Node.HexToByteArray(), cancellationToken: cancellationToken);

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			@event.Node.RemoveHexPrefix(), @event.Node.HexToByteArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.NodeOperator;
				x.Address = @event.Node.HexToByteArray();
			}, cancellationToken: cancellationToken);

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
				MinipoolValidators = [],
				MegapoolValidators = [],
				RPLLegacyStaked = default,
				RPLMegapoolStaked = default,
				WithdrawalAddress = null,
				RPLWithdrawalAddress = null,
				StakeOnBehalfAddresses = new(new FastByteArrayComparer()),
			});

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)@event.Time).DateTime);
		context.Nodes.Data.DailyRegistrations[key] =
			context.Nodes.Data.DailyRegistrations.GetValueOrDefault(key) + 1;
		context.Nodes.Data.TotalNodesCount[key] = context.Nodes.Data.TotalNodesCount.GetLatestValueOrDefault() + 1;
	}
}