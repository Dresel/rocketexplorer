using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketMegapoolDelegate;
using RocketExplorer.Ethereum.RocketMegapoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Minipools;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Core.Nodes;

public class NodesSync(IOptions<SyncOptions> options, Storage storage, ILogger<NodesSync> logger)
	: SyncBase<NodesSyncContext>(options, storage, logger)
{
	protected override async Task HandleBlocksAsync(
		NodesSyncContext context, long fromBlock, long toBlock, long latestBlock,
		CancellationToken cancellationToken = default)
	{
		IEnumerable<IEventLog> nodeAddedEvents = await context.Web3.FilterAsync(
			(ulong)fromBlock, (ulong)toBlock, [typeof(NodeRegisteredEventDTO),],
			context.RocketNodeManagerAddresses, Policy);

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			await eventLog.WhenIsAsync<NodeRegisteredEventDTO>(
				(@event, log, innerCancellationToken) => EventAddNewNodeAsync(
					context, context.RocketNodeManager, @event, innerCancellationToken), cancellationToken);
		}

		IEnumerable<IEventLog> megapoolEvents = await context.Web3.FilterAsync(
			(ulong)fromBlock, (ulong)toBlock,
			[
				typeof(MegapoolValidatorEnqueuedEventDTO), typeof(MegapoolValidatorAssignedEventDTO),
				typeof(MegapoolValidatorDequeuedEventDTO),
			], [], Policy);

		foreach (IEventLog eventLog in megapoolEvents)
		{
			await eventLog.WhenIsAsync<MegapoolValidatorEnqueuedEventDTO>(
				(@event, log, innerCancellationToken) => EventAddNewMegapool(
					context, context.Web3, @event, log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorAssignedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMegapoolAsync(
					context, context.Web3, @event.Megapool, (int)@event.ValidatorId, MinipoolStatus.Staking,
					@event.Time,
					log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorDequeuedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMegapoolAsync(
					context, context.Web3, @event.Megapool, (int)@event.ValidatorId, MinipoolStatus.Dequeued,
					@event.Time,
					log, innerCancellationToken), cancellationToken);
		}
	}

	protected override async Task<NodesSyncContext> LoadContextAsync(
		Web3 web3, RocketStorageService rocketStorage, ReadOnlyDictionary<string, RocketPoolContract> contracts,
		CancellationToken cancellationToken = default)
	{
		long activationHeight = contracts["rocketStorage"].Versions.Single().ActivationHeight;

		Logger.LogInformation("Loading {snapshot}", Keys.NodesSnapshot);
		NodesSnapshot nodesSnapshot =
			await Storage.ReadAsync<NodesSnapshot>(Keys.NodesSnapshot, cancellationToken) ?? new NodesSnapshot
			{
				BlockHeight = activationHeight,
				Index = [],
				DailyRegistrations = [],
				TotalNodeCount = [],
			};

		Logger.LogInformation("Loading {snapshot}", Keys.QueueSnapshot);
		QueueSnapshot queueSnapshot =
			await Storage.ReadAsync<QueueSnapshot>(Keys.QueueSnapshot, cancellationToken) ?? new QueueSnapshot
			{
				BlockHeight = activationHeight,
				StandardIndex = [],
				ExpressIndex = [],
				TotalQueueCount = [],
				DailyEnqueued = [],
				DailyDequeued = [],
				DailyVoluntaryExits = [],
			};

		return new NodesSyncContext
		{
			Web3 = web3,
			CurrentBlockHeight = nodesSnapshot.BlockHeight,
			RocketStorage = rocketStorage,
			Contracts = contracts,
			RocketNodeManager =
				new RocketNodeManagerService(
					web3, await Policy.ExecuteAsync(() => rocketStorage.GetAddressQueryAsync("rocketNodeManager"))),
			RocketNodeManagerAddresses = contracts["rocketNodeManager"].Versions.Select(x => x.Address).ToArray(),
			NodeIndex =
				nodesSnapshot.Index.ToDictionary(
					x => x.ContractAddress.ToHex(true), x => x, StringComparer.OrdinalIgnoreCase),
			DailyRegistrations = nodesSnapshot.DailyRegistrations,
			TotalNodesCount = nodesSnapshot.TotalNodeCount,
			StandardQueue = queueSnapshot.StandardIndex.ToList(),
			ExpressQueue = queueSnapshot.ExpressIndex.ToList(),
			TotalQueueCount = new SortedList<DateOnly, int>(queueSnapshot.TotalQueueCount),
			DailyEnqueued = queueSnapshot.DailyEnqueued,
			DailyDequeued = queueSnapshot.DailyDequeued,
			DailyVoluntaryExits = queueSnapshot.DailyVoluntaryExits,
		};
	}

	protected override async Task SaveContextAsync(
		NodesSyncContext context, CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Writing {snapshot}", Keys.NodesSnapshot);
		await Storage.WriteAsync(
			Keys.NodesSnapshot,
			new NodesSnapshot
			{
				BlockHeight = context.CurrentBlockHeight,
				Index = context.NodeIndex.Values.ToArray(),
				DailyRegistrations = context.DailyRegistrations,
				TotalNodeCount = context.TotalNodesCount,
			}, cancellationToken);

		Logger.LogInformation("Writing {snapshot}", Keys.QueueSnapshot);
		await Storage.WriteAsync(
			Keys.QueueSnapshot,
			new QueueSnapshot
			{
				BlockHeight = context.CurrentBlockHeight,
				TotalQueueCount = context.TotalQueueCount,
				DailyEnqueued = context.DailyEnqueued,
				DailyDequeued = context.DailyDequeued,
				DailyVoluntaryExits = context.DailyVoluntaryExits,
				StandardIndex = context.StandardQueue.ToArray(),
				ExpressIndex = context.ExpressQueue.ToArray(),
			}, cancellationToken);

		foreach (Node node in context.Nodes.Values)
		{
			Logger.LogInformation("Writing {snapshot}", Keys.Node(node.ContractAddress.ToHex(true)));
			await Storage.WriteAsync(Keys.Node(node.ContractAddress.ToHex(true)), node, cancellationToken);
		}

		foreach ((string? megapoolAddress, int megapoolIndex, Minipool? minipool) in context.MegaMinipools.SelectMany(
					megapool => megapool.Value.Select(index => (megapool.Key, index.Key, index.Value))))
		{
			Logger.LogInformation("Writing {snapshot}", Keys.MegapoolMinipool(megapoolAddress, megapoolIndex));
			await Storage.WriteAsync(
				Keys.MegapoolMinipool(megapoolAddress, megapoolIndex), minipool, cancellationToken);
		}
	}

	private async Task EventAddNewMegapool(
		NodesSyncContext context, Web3 web3, MegapoolValidatorEnqueuedEventDTO @event, FilterLog log,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = @event.Megapool;
		RocketMegapoolDelegateService megapoolDelegate = new(web3, megapoolAddress);

		context.MegapoolNodeOperatorMap.TryGetValue(megapoolAddress, out string? nodeOperatorAddress);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			nodeOperatorAddress = await FetchNodeOperatorAddress(
				context, log, megapoolAddress, megapoolDelegate, cancellationToken);

			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
			{
				return;
			}

			context.MegapoolNodeOperatorMap[megapoolAddress] = nodeOperatorAddress;

			context.NodeIndex[nodeOperatorAddress] = context.NodeIndex[nodeOperatorAddress] with
			{
				MegapoolAddress = megapoolAddress.HexToByteArray(),
			};

			context.Nodes[nodeOperatorAddress] = context.Nodes[nodeOperatorAddress] with
			{
				MegapoolAddress = megapoolAddress.HexToByteArray(),
			};
		}

		GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(
			(uint)@event.ValidatorId, new BlockParameter(log.BlockNumber));

		Minipool minipool = new()
		{
			NodeOperatorAddress = nodeOperatorAddress.HexToByteArray(),
			MegapoolAddress = megapoolAddress.HexToByteArray(),
			MegapoolIndex = (int)@event.ValidatorId,
			ExpressTicketUsed = validatorInfo.ReturnValue1.ExpressUsed,
			PubKey = validatorInfo.ReturnValue1.PubKey,
			CreationTimestamp = (ulong)@event.Time,
			Status = MinipoolStatus.InQueue,
			Bond = 4, // TODO: Saturn2
			Type = MinipoolType.Megapool,
		};

		context.MegaMinipools.TryAdd(megapoolAddress, []);
		context.MegaMinipools[megapoolAddress][minipool.MegapoolIndex.Value] = minipool;

		// TODO: Use list
		MinipoolIndexEntry entry = new()
		{
			CreationTimestamp = (long)minipool.CreationTimestamp,
			NodeAddress = minipool.NodeOperatorAddress,
			PubKey = minipool.PubKey,
			MegapoolAddress = megapoolAddress.HexToByteArray(),
			MegapoolIndex = minipool.MegapoolIndex,
		};

		context.Nodes[nodeOperatorAddress].MegaMinipools =
		[
			..context.Nodes[nodeOperatorAddress].MegaMinipools, entry,
		];

		if (!validatorInfo.ReturnValue1.ExpressUsed)
		{
			context.StandardQueue.Add(entry);
		}
		else
		{
			context.ExpressQueue.Add(entry);
		}

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)@event.Time).DateTime);

		context.TotalQueueCount[key] = context.TotalQueueCount.GetLatestOrDefault() + 1;
		context.DailyEnqueued[key] = context.DailyEnqueued.GetValueOrDefault(key) + 1;
	}

	private async Task EventAddNewNodeAsync(
		NodesSyncContext context,
		RocketNodeManagerService latestRocketNodeManager,
		NodeRegisteredEventDTO @event, CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Node registered {Address}", @event.Node);

		context.NodeIndex[@event.Node] = new NodeIndexEntry
		{
			ContractAddress = @event.Node.HexToByteArray(),
			RegistrationTimestamp = (long)@event.Time,
		};

		// Fetch latest node details (otherwise we would have to use the current latestRocketNodeManager of log.BlockNumber via contractsSnapshot)
		GetNodeDetailsOutputDTO? nodeDetails = null;

		try
		{
			// TODO: Obsolete, replace
			// See https://discord.com/channels/405159462932971535/704214664829075506/1365495113383677973
			nodeDetails = await Policy.ExecuteAsync(
				() => latestRocketNodeManager.GetNodeDetailsQueryAsync(@event.Node));
		}
		catch
		{
			Logger.LogWarning("Failed to fetch node details for {Node}", @event.Node);
		}

		// TODO: Add more details
		context.Nodes[@event.Node] = new Node
		{
			ContractAddress = @event.Node.HexToByteArray(),
			RegistrationTimestamp = (long)@event.Time,
			Timezone = nodeDetails?.NodeDetails.TimezoneLocation ?? "Unknown",
		};

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)@event.Time).DateTime);
		context.DailyRegistrations[key] = context.DailyRegistrations.GetValueOrDefault(key) + 1;
		context.TotalNodesCount[key] = context.TotalNodesCount.GetLatestOrDefault() + 1;
	}

	private async Task EventUpdateMegapoolAsync(
		NodesSyncContext context, Web3 web3, string megapoolAddress, int validatorId, MinipoolStatus status,
		BigInteger eventTime,
		FilterLog log, CancellationToken cancellationToken)
	{
		RocketMegapoolDelegateService megapoolDelegate = new(web3, megapoolAddress);

		context.MegapoolNodeOperatorMap.TryGetValue(megapoolAddress, out string? nodeOperatorAddress);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			nodeOperatorAddress = await FetchNodeOperatorAddress(
				context, log, megapoolAddress, megapoolDelegate, cancellationToken);

			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
			{
				return;
			}

			context.MegapoolNodeOperatorMap[megapoolAddress] = nodeOperatorAddress;
		}

		context.MegaMinipools.TryAdd(megapoolAddress, []);

		if (!context.MegaMinipools[megapoolAddress].ContainsKey(validatorId))
		{
			context.MegaMinipools[megapoolAddress][validatorId] = await Storage.ReadAsync<Minipool>(
					Keys.MegapoolMinipool(megapoolAddress, validatorId), cancellationToken) ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.MegaMinipools[megapoolAddress][validatorId].Status = status;

		if (status == MinipoolStatus.Staking || status == MinipoolStatus.Dequeued)
		{
			int h = 0;

			h += context.StandardQueue.RemoveAll(
				x => x.PubKey == context.MegaMinipools[megapoolAddress][validatorId].PubKey);
			h += context.ExpressQueue.RemoveAll(
				x => x.PubKey == context.MegaMinipools[megapoolAddress][validatorId].PubKey);

			Debug.Assert(h == 1, "Only one element should be removed");

			Dictionary<DateOnly, int> dictionary =
				status == MinipoolStatus.Staking ? context.DailyDequeued : context.DailyVoluntaryExits;
			DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventTime).DateTime);
			dictionary[key] = dictionary.GetValueOrDefault(key) + 1;

			context.TotalQueueCount[key] = context.TotalQueueCount.GetLatestOrDefault() - 1;
		}
	}

	private async Task<string?> FetchNodeOperatorAddress(
		NodesSyncContext context, FilterLog log, string megapoolAddress,
		RocketMegapoolDelegateService megapoolDelegate, CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress =
			await megapoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(log.BlockNumber));

		// If not found might be megapool from different rocket pool version
		if (!context.NodeIndex.ContainsKey(nodeOperatorAddress))
		{
			logger.LogWarning(
				"Node operator {NodeOperatorAddress} for {Megapool} not found in index.", nodeOperatorAddress,
				megapoolAddress);
			return null;
		}

		// Can happen if the same node operator address is used for multiple rocket pool deployments
		if (await context.RocketNodeManager.GetMegapoolAddressQueryAsync(nodeOperatorAddress) != megapoolAddress)
		{
			logger.LogWarning(
				"Node operator {NodeOperatorAddress} found in index but megapool address {Megapool} does not match.",
				nodeOperatorAddress, megapoolAddress);
			return null;
		}

		if (!context.Nodes.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes[nodeOperatorAddress] =
				await Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken) ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		return nodeOperatorAddress;
	}
}