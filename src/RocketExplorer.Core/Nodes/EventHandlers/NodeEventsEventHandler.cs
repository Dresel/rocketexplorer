using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
using RocketExplorer.Ethereum.rocketStorage.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class NodeEventsEventHandler
{
	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeWithdrawalAddressSetEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			eventLog.Event.WithdrawalAddress.RemoveHexPrefix(),
			eventLog.Event.WithdrawalAddress.HexToByteArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.WithdrawalAddress;
				x.Address = eventLog.Event.WithdrawalAddress.HexToByteArray();
				x.NodeAddresses.Add(node.ContractAddress);
			}, cancellationToken: cancellationToken);

		node.WithdrawalAddress = eventLog.Event.WithdrawalAddress.HexToByteArray();
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

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
		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		string stakeOnBehalfAddress = eventLog.Event.Caller;

		if (eventLog.Event.Allowed)
		{
			node.StakeOnBehalfAddresses.Add(stakeOnBehalfAddress.HexToByteArray());

			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				stakeOnBehalfAddress.RemoveHexPrefix(), stakeOnBehalfAddress.HexToByteArray(),
				new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
				x =>
				{
					x.Type |= IndexEntryType.StakeOnBehalfAddress;
					x.Address = stakeOnBehalfAddress.HexToByteArray();
					x.NodeAddresses.Add(node.ContractAddress);
				}, cancellationToken: cancellationToken);

			await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
				stakeOnBehalfAddress.HexToByteArray(), cancellationToken: cancellationToken);

			globalContext.GetLogger<NodeEventsEventHandler>()
				.LogInformation(
					"Node {NodeAddress} stake on behalf address {StakeOnBehalfAddress} added", nodeOperatorAddress,
					stakeOnBehalfAddress);
		}
		else
		{
			node.StakeOnBehalfAddresses.RemoveWhere(x =>
				new FastByteArrayComparer().Equals(x, stakeOnBehalfAddress.HexToByteArray()));

			_ = globalContext.Services.GlobalIndexService.UpdateEntryAsync(
				stakeOnBehalfAddress.RemoveHexPrefix(), stakeOnBehalfAddress.HexToByteArray(),
				new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
				x =>
				{
					x.Type &= ~IndexEntryType.StakeOnBehalfAddress;
					x.Address = stakeOnBehalfAddress.HexToByteArray();

					FastByteArrayComparer comparer = new FastByteArrayComparer();
					int index = x.NodeAddresses.FindIndex(bytes => comparer.Equals(bytes, node.ContractAddress));
					x.NodeAddresses.RemoveAt(index);
				}, cancellationToken: cancellationToken);

			await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
				stakeOnBehalfAddress.HexToByteArray(), cancellationToken: cancellationToken);

			globalContext.GetLogger<NodeEventsEventHandler>()
				.LogInformation(
					"Node {NodeAddress} stake on behalf address {StakeOnBehalfAddress} removed", nodeOperatorAddress,
					stakeOnBehalfAddress);
		}

		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeSmoothingPoolStateChangedEventDTOBase> eventLog,
		CancellationToken cancellationToken)
	{
		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		node.InSmoothingPool = eventLog.Event.State;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

		globalContext.GetLogger<NodeEventsEventHandler>()
			.LogInformation(
				"Node {NodeAddress} {SmoothingPoolAction} the smoothing pool", nodeOperatorAddress,
				eventLog.Event.State ? "joined" : "left");
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeTimezoneLocationSetEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		RocketNodeManagerService rocketNodeManagerService = new(
			globalContext.Services.Web3,
			globalContext.Contracts["rocketNodeManager"].Versions
				.Last(x => x.ActivationHeight < (long)eventLog.Log.BlockNumber.Value).Address);

		string timezone = await rocketNodeManagerService.GetNodeTimezoneLocationQueryAsync(
			eventLog.Event.Node, new BlockParameter(eventLog.Log.BlockNumber));

		node.Timezone = timezone;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

		globalContext.GetLogger<NodeEventsEventHandler>()
			.LogInformation("Node {NodeAddress} timezone changed to {timezone}", nodeOperatorAddress, timezone);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeRPLWithdrawalAddressUnsetEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		_ = globalContext.Services.GlobalIndexService.UpdateEntryAsync(
			node.RPLWithdrawalAddress?.ToHex() ?? throw new InvalidOperationException("Withdrawal address should not be null"),
			node.RPLWithdrawalAddress ?? throw new InvalidOperationException("Withdrawal address should not be null"),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type &= ~IndexEntryType.RPLWithdrawalAddress;

				FastByteArrayComparer comparer = new FastByteArrayComparer();
				int index = x.NodeAddresses.FindIndex(bytes => comparer.Equals(bytes, node.ContractAddress));
				x.NodeAddresses.RemoveAt(index);
			}, entry => entry.Type == 0, cancellationToken);

		await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
			node.RPLWithdrawalAddress ??
			throw new InvalidOperationException("Withdrawal address should not be null"),
			cancellationToken: cancellationToken);

		node.RPLWithdrawalAddress = null;
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

		globalContext.GetLogger<NodeEventsEventHandler>()
			.LogInformation("Node {NodeAddress} RPL withdrawal address unset", nodeOperatorAddress);
	}

	public static async Task HandleAsync(
		GlobalContext globalContext, EventLog<NodeRPLWithdrawalAddressSetEventDTO> eventLog,
		CancellationToken cancellationToken)
	{
		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		string nodeOperatorAddress = eventLog.Event.Node;

		if (!context.Nodes.Data.Nodes.TryGetValue(nodeOperatorAddress, out NodeMasterInfo? node))
		{
			globalContext.GetLogger<NodeEventsEventHandler>().LogError(
				"Node operator {NodeOperatorAddress} not found in index.", nodeOperatorAddress);
			return;
		}

		_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
			eventLog.Event.WithdrawalAddress.RemoveHexPrefix(), eventLog.Event.WithdrawalAddress.HexToByteArray(),
			new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
			x =>
			{
				x.Type |= IndexEntryType.RPLWithdrawalAddress;
				x.Address = eventLog.Event.WithdrawalAddress.HexToByteArray();
				x.NodeAddresses.Add(node.ContractAddress);
			}, cancellationToken: cancellationToken);

		node.RPLWithdrawalAddress = eventLog.Event.WithdrawalAddress.HexToByteArray();
		context.Nodes.NodesUpdated.Add(nodeOperatorAddress);

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

		NodesMasterContext context = await globalContext.NodesMasterContextFactory;

		globalContext.DashboardContext.NodeOperators++;

		RocketNodeManagerService rocketNodeManagerService = new(
			globalContext.Services.Web3,
			globalContext.Contracts["rocketNodeManager"].Versions
				.Last(x => x.ActivationHeight < (long)eventLog.Log.BlockNumber.Value).Address);

		string timezone = await rocketNodeManagerService.GetNodeTimezoneLocationQueryAsync(
			@event.Node, new BlockParameter(eventLog.Log.BlockNumber));

		context.Nodes.Data.Nodes.Add(
			@event.Node, new NodeMasterInfo
			{
				ContractAddress = @event.Node.HexToByteArray(),
				RegistrationTimestamp = (long)@event.Time,
				Timezone = timezone,
			});
		context.Nodes.NodesUpdated.Add(@event.Node);

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

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)@event.Time).DateTime);
		context.Nodes.Data.DailyRegistrations[key] =
			context.Nodes.Data.DailyRegistrations.GetValueOrDefault(key) + 1;
		context.Nodes.Data.TotalNodesCount[key] = context.Nodes.Data.TotalNodesCount.GetLatestValueOrDefault() + 1;
	}
}
