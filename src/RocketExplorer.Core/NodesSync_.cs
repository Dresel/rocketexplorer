//using System.Diagnostics;
//using System.Numerics;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using Nethereum.Contracts;
//using Nethereum.Hex.HexConvertors.Extensions;
//using Nethereum.JsonRpc.Client;
//using Nethereum.RPC.Eth.DTOs;
//using Nethereum.Web3;
//using RocketExplorer.Ethereum;
//using RocketExplorer.Ethereum.RocketMegapoolDelegate;
//using RocketExplorer.Ethereum.RocketMegapoolDelegate.ContractDefinition;
//using RocketExplorer.Ethereum.RocketNodeManager;
//using RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition;
//using RocketExplorer.Ethereum.RocketStorage;
//using RocketExplorer.Shared.Contracts;
//using RocketExplorer.Shared.Minipools;
//using RocketExplorer.Shared.Nodes;

//namespace RocketExplorer.Core;

//public class NodesSync(IOptions<SyncOptions> options, Storage storage, ILogger<NodesSync> logger)
//	: SyncBase(options, storage, logger)
//{
//	public async Task UpdateAndPublishAsync(CancellationToken cancellationToken = default) =>

//		//NodesSnapshot snapshot = await Storage.ReadAsync<NodesSnapshot>("nodes-snapshot.msgpack", cancellationToken);
//		await UpdateSnapshotAsync(cancellationToken);

//	//private async Task<Dictionary<string, Node>> BootstrapNodesAsync(
//	//	Web3 web3, RocketStorageService rocketStorage, ulong latestBlock)
//	//{
//	//	string rocketNodeManagerContractAddress = await Policy.ExecuteAsync(
//	//		() => rocketStorage.GetAddressQueryAsync("rocketNodeManager"));
//	//	RocketNodeManagerService rocketNodeManagerService = new(web3, rocketNodeManagerContractAddress);

//	//	BigInteger count = await Policy.ExecuteAsync(
//	//		() => rocketNodeManagerService.GetNodeCountQueryAsync(new BlockParameter(latestBlock)));

//	//	Dictionary<string, Node> results = [];

//	//	for (int i = 0; i < count; i += 100)
//	//	{
//	//		List<string> nodeAddresses = await Policy.ExecuteAsync(
//	//			() => rocketNodeManagerService.GetNodeAddressesQueryAsync(i, 100, new BlockParameter(latestBlock)));

//	//		foreach (string nodeAddress in nodeAddresses)
//	//		{
//	//			GetNodeDetailsOutputDTO nodeDetails = await Policy.ExecuteAsync(
//	//				() => rocketNodeManagerService.GetNodeDetailsQueryAsync(
//	//					nodeAddress, new BlockParameter(latestBlock)));

//	//			results[nodeAddress] = new Node
//	//			{
//	//				ContractAddress = nodeAddress.HexToByteArray(),
//	//				RegistrationTimestamp = (ulong)nodeDetails.NodeDetails.RegistrationTime,
//	//				Timezone = nodeDetails.NodeDetails.TimezoneLocation,
//	//				// TODO: Timezone, ENS,
//	//			};
//	//		}
//	//	}

//	//	return results;

//	//	return [];
//	//}

//	// Enqueued / Assigned / Dequeued per day / month

//	// Current queue? (Combined and Legacy, Normal, Express)
//	// Big numbers: Legacy / Normal / Express

//	// Minipools per Megapool
//	// Pub Id
//	// Current Status
//	// => Wen enqueued, wen assigned, wen dequeued
//	// Express
//	// Current calculated Queue position?

//	public async Task<NodesSnapshot> UpdateSnapshotAsync(CancellationToken cancellationToken = default)
//	{
//		// TODO: Use CancellationToken
//		Logger.LogInformation(
//			"Using Rocket Pool environment {Environment} with rpc endpoint {RPCUrl}", Options.Environment,
//			Options.RPCUrl);

//		RpcClient rpcClient = new(new Uri(Options.RPCUrl), new HttpClient { Timeout = TimeSpan.FromSeconds(60), });
//		Web3 web3 = new(rpcClient);
//		ulong latestBlock =
//			(ulong)(await Policy.ExecuteAsync(() => web3.Eth.Blocks.GetBlockNumber.SendRequestAsync())).Value;

//		//NodesSnapshot nodesSnapshot =
//		//	await Storage.ReadAsync<NodesSnapshot>("nodes-snapshot.msgpack", cancellationToken) ?? new NodesSnapshot
//		//	{
//		//		// TODO: Get currentBlock from contract infos
//		//		BlockHeight = 1317323,
//		//	};

//		//QueueSnapshot queueSnapshot =
//		//	await Storage.ReadAsync<QueueSnapshot>("queue-snapshot.msgpack", cancellationToken) ?? new QueueSnapshot
//		//	{
//		//		// TODO: Get currentBlock from contract infos
//		//		BlockHeight = 1317323,
//		//	};

//		NodesSnapshot nodesSnapshot = new NodesSnapshot
//		{
//			// TODO: Get currentBlock from contract infos
//			BlockHeight = 1317323,
//		};

//		QueueSnapshot queueSnapshot = new QueueSnapshot
//		{
//			// TODO: Get currentBlock from contract infos
//			BlockHeight = 1317323,
//		};

//		Logger.LogInformation("Processing block from {FromBlock} to {ToBlock}", nodesSnapshot.BlockHeight, latestBlock);

//		if (nodesSnapshot.BlockHeight == latestBlock)
//		{
//			return nodesSnapshot;
//		}

//		RocketStorageService rocketStorage = new(web3, Options.RocketStorageContractAddress);

//		ulong currentBlock = nodesSnapshot.BlockHeight;

//		string rocketNodeManagerContractAddress = await Policy.ExecuteAsync(
//			() => rocketStorage.GetAddressQueryAsync("rocketNodeManager"));
//		RocketNodeManagerService rocketNodeManagerService = new(web3, rocketNodeManagerContractAddress);

//		// TODO: Init from snapshot
//		NodesSyncContext context = new()
//		{
//			NodeIndex = nodesSnapshot.Index.ToDictionary(x => x.ContractAddress.ToHex(true), x => x, StringComparer.OrdinalIgnoreCase),
//			DailyRegistrations = nodesSnapshot.DailyRegistrations.ToDictionary(),
//			StandardQueue = queueSnapshot.StandardIndex.ToList(),
//			ExpressQueue = queueSnapshot.StandardIndex.ToList(),
//			TotalQueueCount = new SortedList<DateOnly, int>(queueSnapshot.DailyEnqueued),
//			DailyEnqueued = queueSnapshot.DailyEnqueued.ToDictionary(),
//			DailyDequeued = queueSnapshot.DailyDequeued.ToDictionary(),
//			DailyVoluntaryExits = queueSnapshot.DailyVoluntaryExits.ToDictionary(),
//		};

//		ContractsSnapshot contracts =
//			await Storage.ReadAsync<ContractsSnapshot>("contracts-snapshot.msgpack", cancellationToken) ??
//			throw new InvalidOperationException("Cannot read contracts snapshot from storage.");

//		ulong toBlock;

//		// Filter processing
//		do
//		{
//			toBlock = Math.Min(currentBlock + 25000, latestBlock);

//			Logger.LogDebug("Processing block {FromBlock} to {ToBlock}", currentBlock, toBlock);

//			IEnumerable<IEventLog> nodeAddedEvents = await web3.FilterAsync(currentBlock, toBlock, [typeof(NodeRegisteredEventDTO),],
//				contracts.Contracts.Single(x => x.Name == "rocketNodeManager").Versions.Select(x => x.Address).ToList(),
//				Policy);

//			foreach (IEventLog eventLog in nodeAddedEvents)
//			{
//				await eventLog.WhenIsAsync<NodeRegisteredEventDTO>(
//					(@event, log) => EventAddNewNode(context, rocketNodeManagerService, @event));
//			}

//			IEnumerable<IEventLog> megapoolEvents = await web3.FilterAsync(currentBlock, toBlock,
//				[typeof(MegapoolValidatorEnqueuedEventDTO), typeof(MegapoolValidatorAssignedEventDTO), typeof(MegapoolValidatorDequeuedEventDTO),], [], Policy);

//			foreach (IEventLog eventLog in megapoolEvents)
//			{
//				await eventLog.WhenIsAsync<MegapoolValidatorEnqueuedEventDTO>(
//					(@event, log) => EventAddNewMegapool(context, web3, @event, log));

//				await eventLog.WhenIsAsync<MegapoolValidatorAssignedEventDTO>(
//					(@event, log) => EventUpdateMegapoolAsync(
//						context, web3, @event.Megapool, (int)@event.ValidatorId, MinipoolStatus.Staking, @event.Time,
//						log));

//				await eventLog.WhenIsAsync<MegapoolValidatorDequeuedEventDTO>(
//					(@event, log) => EventUpdateMegapoolAsync(
//						context, web3, @event.Megapool, (int)@event.ValidatorId, MinipoolStatus.Dequeued, @event.Time,
//						log));
//			}

//			currentBlock = toBlock + 1;
//		}
//		while (currentBlock <= latestBlock);

//		await Storage.WriteAsync(
//			"nodes-snapshot.msgpack",
//			new NodesSnapshot
//			{
//				BlockHeight = toBlock,
//				Index = context.NodeIndex.Values.ToArray(),
//				DailyRegistrations = context.DailyRegistrations.ToDictionary(),
//			}, cancellationToken);

//		await Storage.WriteAsync(
//			"queue-snapshot.msgpack",
//			new QueueSnapshot()
//			{
//				BlockHeight = toBlock,
//				TotalQueueCount = context.TotalQueueCount,
//				DailyEnqueued = context.DailyEnqueued,
//				DailyDequeued = context.DailyDequeued,
//				DailyVoluntaryExits = context.DailyVoluntaryExits,
//				StandardIndex = context.StandardQueue.ToArray(),
//				ExpressIndex = context.ExpressQueue.ToArray(),
//			}, cancellationToken);

//		foreach (Node node in context.Nodes.Values)
//		{
//			await Storage.WriteAsync($"nodes/{node.ContractAddress.ToHex(true)}.msgpack", node, cancellationToken);
//		}

//		return null;
//	}

//	private async Task EventAddNewMegapool(
//		NodesSyncContext context, Web3 web3, MegapoolValidatorEnqueuedEventDTO @event, FilterLog log)
//	{
//		string megapoolAddress = @event.Megapool;
//		context.MegaMinipools.TryAdd(megapoolAddress, []);

//		RocketMegapoolDelegateService megapoolDelegate = new(web3, megapoolAddress);

//		context.MegapoolNodeOperatorMap.TryGetValue(megapoolAddress, out string? nodeOperatorAddress);

//		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
//		{
//			nodeOperatorAddress = await FetchNodeOperatorAddress(context, log, megapoolAddress, megapoolDelegate);

//			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
//			{
//				return;
//			}

//			context.Nodes[nodeOperatorAddress] = context.Nodes[nodeOperatorAddress] with
//			{
//				MegapoolAddress = megapoolAddress.HexToByteArray(),
//			};
//		}

//		GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(
//			(uint)@event.ValidatorId, new BlockParameter(log.BlockNumber));

//		Minipool minipool = new()
//		{
//			NodeOperatorAddress = nodeOperatorAddress.HexToByteArray(),
//			MegapoolAddress = megapoolAddress.HexToByteArray(),
//			MegapoolIndex = (uint)@event.ValidatorId,
//			ExpressTicketUsed = validatorInfo.ReturnValue1.ExpressUsed,
//			PubKey = validatorInfo.ReturnValue1.PubKey,
//			CreationTimestamp = (ulong)@event.Time,
//			Status = MinipoolStatus.Created,
//			Bond = 4, // TODO: Saturn2
//			Type = MinipoolType.Megapool,
//		};

//		context.MegaMinipools[megapoolAddress].Add(minipool);
//		context.Nodes[nodeOperatorAddress].MegaMinipools =
//			[..context.Nodes[nodeOperatorAddress].MegaMinipools, minipool,];

//		if (!validatorInfo.ReturnValue1.ExpressUsed)
//		{
//			context.StandardQueue.Add(
//				new NodeIndexEntry
//				{
//					RegistrationTimestamp = (ulong)@event.Time, ContractAddress = validatorInfo.ReturnValue1.PubKey,
//				});
//		}
//		else
//		{
//			context.ExpressQueue.Add(
//				new NodeIndexEntry
//				{
//					RegistrationTimestamp = (ulong)@event.Time, ContractAddress = validatorInfo.ReturnValue1.PubKey,
//				});
//		}

//		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)@event.Time).DateTime);

//		context.TotalQueueCount[key] = context.TotalQueueCount.GetValueOrLastOrDefault(key) + 1;
//		context.DailyEnqueued[key] = context.DailyEnqueued.GetValueOrDefault(key) + 1;
//	}

//	//await Storage.WriteAsync("nodes-snapshot.msgpack", snapshot, cancellationToken);

//	private async Task EventAddNewNode(
//		NodesSyncContext context,
//		RocketNodeManagerService latestRocketNodeManagerService,
//		NodeRegisteredEventDTO @event)
//	{
//		// Node, Index, Megapool Address
//		context.NodeIndex[@event.Node] = new NodeIndexEntry
//		{
//			ContractAddress = @event.Node.HexToByteArray(), RegistrationTimestamp = (ulong)@event.Time,
//		};

//		// Fetch latest node details (otherwise we would have to use the current rocketNodeManagerService of log.BlockNumber via contractsSnapshot)

//		// TODO: Reactivate on hoodie
//		////GetNodeDetailsOutputDTO nodeDetails = await Policy.ExecuteAsync(
//		////	() => latestRocketNodeManagerService.GetNodeDetailsQueryAsync(@event.Node));

//		// TODO: Add more details
//		context.Nodes[@event.Node] = new Node
//		{
//			ContractAddress = @event.Node.HexToByteArray(),
//			RegistrationTimestamp = (long)@event.Time,
//			Timezone = "Unknown", ////.NodeDetails.TimezoneLocation,
//		};

//		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)@event.Time).DateTime);
//		context.DailyRegistrations[key] = context.DailyRegistrations.GetValueOrDefault(key) + 1;
//	}

//	private async Task EventUpdateMegapoolAsync(
//		NodesSyncContext context, Web3 web3, string megapoolAddress, int validatorId, MinipoolStatus status,
//		BigInteger eventTime,
//		FilterLog log)
//	{
//		context.MegaMinipools.TryAdd(megapoolAddress, []);

//		RocketMegapoolDelegateService megapoolDelegate = new(web3, megapoolAddress);

//		context.MegapoolNodeOperatorMap.TryGetValue(megapoolAddress, out string? nodeOperatorAddress);

//		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
//		{
//			nodeOperatorAddress = await FetchNodeOperatorAddress(context, log, megapoolAddress, megapoolDelegate);

//			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
//			{
//				return;
//			}

//			context.MegaMinipools[megapoolAddress] = context.Nodes[nodeOperatorAddress].MegaMinipools.ToList();
//		}

//		context.MegaMinipools[megapoolAddress][validatorId].Status = status;

//		if (status == MinipoolStatus.Staking || status == MinipoolStatus.Dequeued)
//		{
//			int h = 0;
//			h += context.StandardQueue.RemoveAll(
//				x => x.ContractAddress == context.MegaMinipools[megapoolAddress][validatorId].PubKey);
//			h += context.ExpressQueue.RemoveAll(
//				x => x.ContractAddress == context.MegaMinipools[megapoolAddress][validatorId].PubKey);

//			Debug.Assert(h == 1, "Only one element should be removed");

//			Dictionary<DateOnly, int> dictionary =
//				status == MinipoolStatus.Staking ? context.DailyDequeued : context.DailyVoluntaryExits;
//			DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventTime).DateTime);
//			dictionary[key] = dictionary.GetValueOrDefault(key) + 1;

//			context.TotalQueueCount[key] = context.TotalQueueCount.GetValueOrLastOrDefault(key) - 1;
//		}
//	}

//	private async Task<string?> FetchNodeOperatorAddress(
//		NodesSyncContext context, FilterLog log, string megapoolAddress,
//		RocketMegapoolDelegateService megapoolDelegate)
//	{
//		string nodeOperatorAddress =
//			await megapoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(log.BlockNumber));

//		// If not found might be megapool from different rocket pool version
//		if (!context.NodeIndex.ContainsKey(nodeOperatorAddress))
//		{
//			logger.LogWarning(
//				"Node operator {NodeOperatorAddress} for {Megapool} not found in index.", nodeOperatorAddress,
//				megapoolAddress);
//			return null;
//		}

//		if (!context.Nodes.ContainsKey(nodeOperatorAddress))
//		{
//			context.Nodes[nodeOperatorAddress] =
//				await Storage.ReadAsync<Node>($"nodes/{nodeOperatorAddress}.msgpack") ??
//				throw new InvalidOperationException("Cannot read node operator from storage.");
//		}

//		context.MegapoolNodeOperatorMap[megapoolAddress] = nodeOperatorAddress;

//		return nodeOperatorAddress;
//	}
//}

//////BigInteger count = await Policy.ExecuteAsync(
//////	() => rocketNodeManagerService.GetNodeCountQueryAsync(new BlockParameter(latestBlock)));

//////List<string> nodeAddresses = await Policy.ExecuteAsync(
//////	() => rocketNodeManagerService.GetNodeAddressesQueryAsync(0, 100, new BlockParameter(latestBlock)));

//////string rocketMegapoolDelegateAddress = await Policy.ExecuteAsync(
//////	() => rocketStorage.GetAddressQueryAsync("rocketMegapoolDelegate"));

//////ulong currentBlock = snapshot.BlockHeight;

////string addressQueueStorageContractAddress = await Policy.ExecuteAsync(
////	() => rocketStorage.GetAddressQueryAsync("addressQueueStorage"));
////AddressQueueStorageService addressQueueStorageService = new(web3, addressQueueStorageContractAddress);

////string linkedListStorageContractAddress = await Policy.ExecuteAsync(
////	() => rocketStorage.GetAddressQueryAsync("linkedListStorage"));
////LinkedListStorageService linkedListStorageService = new(web3, linkedListStorageContractAddress);

////// Bootstrap
////if (snapshot.BlockHeight == 0)
////{
////	List<NodeIndexEntry> index = new();

////	BigInteger count = await Policy.ExecuteAsync(
////		() => rocketNodeManagerService.GetNodeCountQueryAsync(new BlockParameter(latestBlock)));

////	for (int i = 0; i < count; i += 100)
////	{
////		List<string> nodeAddresses = await Policy.ExecuteAsync(
////			() => rocketNodeManagerService.GetNodeAddressesQueryAsync(i, 100, new BlockParameter(latestBlock)));

////		foreach (string nodeAddress in nodeAddresses)
////		{
////			GetNodeDetailsOutputDTO nodeDetails = await Policy.ExecuteAsync(
////				() => rocketNodeManagerService.GetNodeDetailsQueryAsync(
////					nodeAddress, new BlockParameter(latestBlock)));

////			// TODO: ENS
////			index.Add(
////				new NodeIndexEntry
////				{
////					Address = nodeAddress,
////					UnixTimestamp = (ulong)nodeDetails.NodeDetails.RegistrationTime,
////					MinipoolCount = (uint)nodeDetails.NodeDetails.MinipoolCount,
////				});

////			string? megapoolAddress = await Policy.ExecuteAsync(() => rocketNodeManagerService.GetMegapoolAddressQueryAsync(nodeAddress, new BlockParameter(latestBlock)));

//// event MinipoolCreated(address indexed minipool, address indexed node, uint256 time);

//// MinipoolCreation
////			// MinipoolEnqueued
////			// MinipoolDequeued
////			// MinipoolRemoved (dissolved)

////			// MegapoolValidatorEnqueued => Creation
////			// MegapoolValidatorAssigned => Dequeued, Prestake
////			// MegapoolValidatorDequeued (voluntarily exit?)

////			if (megapoolAddress != null && megapoolAddress != "0x0000000000000000000000000000000000000000")
////			{
////				RocketMegapoolDelegateService megapoolDelegate = new RocketMegapoolDelegateService(web3, megapoolAddress);
////				uint validatorCount = await megapoolDelegate.GetValidatorCountQueryAsync(new BlockParameter(latestBlock));
////				GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(0, new BlockParameter(latestBlock));

////			}
////		}
////	}
////}

////BigInteger queueLength1 = await addressQueueStorageService.GetLengthQueryAsync("minipools.available.variable".Sha3(), new BlockParameter(latestBlock));
////BigInteger queueLength2 = await linkedListStorageService.GetLengthQueryAsync("deposit.queue.standard".Sha3(), new BlockParameter(latestBlock));
////BigInteger queueLength3 = await linkedListStorageService.GetLengthQueryAsync("deposit.queue.express".Sha3(), new BlockParameter(latestBlock));

////var x1 = await linkedListStorageService.GetHeadIndexQueryAsync(
////	"deposit.queue.standard".Sha3(), new BlockParameter(latestBlock));
////var x2 = await linkedListStorageService.GetHeadIndexQueryAsync(
////	"deposit.queue.express".Sha3(), new BlockParameter(latestBlock));

////ScanOutputDTO queue1 = await linkedListStorageService.ScanQueryAsync("deposit.queue.standard".Sha3(), 0, queueLength2, new BlockParameter(latestBlock));
////ScanOutputDTO queue1 = await linkedListStorageService.ScanQueryAsync("deposit.queue.express".Sha3(), 0, queueLength2, new BlockParameter(latestBlock));

////for (int i = 0; i < queueLength2; i++)
////{
////	GetItemOutputDTO minipool = await linkedListStorageService.GetItemQueryAsync("deposit.queue.standard".Sha3(), i, new BlockParameter(latestBlock));
////}

//// Queue
//// Node Operator Address, Megapool Address, Joined, Bond, Queue Type, Local Index, Global Index

////// Bootstrap
////if (snapshot.BlockHeight == 0)
////{
////	List<NodeIndexEntry> index = new();

////	BigInteger count = await Policy.ExecuteAsync(
////		() => rocketNodeManagerService.GetNodeCountQueryAsync(new BlockParameter(latestBlock)));

////	for (int i = 0; i < count; i += 100)
////	{
////		List<string> nodeAddresses = await Policy.ExecuteAsync(
////			() => rocketNodeManagerService.GetNodeAddressesQueryAsync(i, 100, new BlockParameter(latestBlock)));

////		foreach (string nodeAddress in nodeAddresses)
////		{
////			GetNodeDetailsOutputDTO nodeDetails = await Policy.ExecuteAsync(
////				() => rocketNodeManagerService.GetNodeDetailsQueryAsync(
////					nodeAddress, new BlockParameter(latestBlock)));

////			// TODO: ENS
////			index.Add(
////				new NodeIndexEntry
////				{
////					Address = nodeAddress,
////					UnixTimestamp = (ulong)nodeDetails.NodeDetails.RegistrationTime,
////					MinipoolCount = (uint)nodeDetails.NodeDetails.MinipoolCount,
////				});

////			string? megapoolAddress = await Policy.ExecuteAsync(() => rocketNodeManagerService.GetMegapoolAddressQueryAsync(nodeAddress, new BlockParameter(latestBlock)));

//// event MinipoolCreated(address indexed minipool, address indexed node, uint256 time);

//// MinipoolCreation
////			// MinipoolEnqueued
////			// MinipoolDequeued
////			// MinipoolRemoved (dissolved)

////			// MegapoolValidatorEnqueued => Creation
////			// MegapoolValidatorAssigned => Dequeued, Prestake
////			// MegapoolValidatorDequeued (voluntarily exit?)

////			if (megapoolAddress != null && megapoolAddress != "0x0000000000000000000000000000000000000000")
////			{
////				RocketMegapoolDelegateService megapoolDelegate = new RocketMegapoolDelegateService(web3, megapoolAddress);
////				uint validatorCount = await megapoolDelegate.GetValidatorCountQueryAsync(new BlockParameter(latestBlock));
////				GetValidatorInfoOutputDTO validatorInfo = await megapoolDelegate.GetValidatorInfoQueryAsync(0, new BlockParameter(latestBlock));

////			}
////		}
////	}
////}

//// Pre Filter processing

//// MinipoolCreation
////			// MinipoolEnqueued
////			// MinipoolDequeued
////			// MinipoolRemoved (dissolved)

////			// MegapoolValidatorEnqueued => Creation
////			// MegapoolValidatorAssigned => Dequeued, Prestake
////			// MegapoolValidatorDequeued (voluntarily exit?)