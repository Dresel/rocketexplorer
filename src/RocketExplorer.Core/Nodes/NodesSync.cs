using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using RocketExplorer.Core.Nodes.EventHandlers;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketMegapoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolManager;
using RocketExplorer.Ethereum.RocketMinipoolManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolQueue.ContractDefinition;
using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;
using Validator = RocketExplorer.Shared.Validators.Validator;

namespace RocketExplorer.Core.Nodes;

public class NodesSync(IOptions<SyncOptions> options, Storage storage, ILoggerFactory loggerFactory)
	: SyncBase<NodesSyncContext>(options, storage, loggerFactory.CreateLogger<NodesSync>())
{
	private readonly ILoggerFactory loggerFactory = loggerFactory;

	protected override async Task HandleBlocksAsync(
		NodesSyncContext context, long fromBlock, long toBlock, long latestBlock,
		CancellationToken cancellationToken = default)
	{
		IEnumerable<IEventLog> nodeAddedEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(NodeRegisteredEventDTO),],
			context.RocketNodeManagerAddresses, Policy);

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			await eventLog.WhenIsAsync<NodeRegisteredEventDTO>(
				NodeRegisteredEventHandler.HandleAsync, context, cancellationToken);
		}

		IEnumerable<IEventLog> minipoolCreatedEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(MinipoolCreatedEventDTO),],
			context.ValidatorInfo.RocketMinipoolManagerAddresses, Policy);

		foreach (IEventLog eventLog in minipoolCreatedEvents)
		{
			await eventLog.WhenIsAsync<MinipoolCreatedEventDTO>(
				MinipoolCreatedEventHandler.HandleAsync, context, cancellationToken);
		}

		List<IEventLog> validatorEvents = (await context.Web3.FilterAsync(
			fromBlock, toBlock,
			[
				typeof(MegapoolValidatorEnqueuedEventDTO),
				typeof(MegapoolValidatorAssignedEventDTO),
				typeof(MegapoolValidatorDequeuedEventDTO),
				typeof(MinipoolPrestakedEventDTO),
				typeof(MinipoolEnqueuedEventDTO),
				typeof(MinipoolDequeuedEventDTO),
				typeof(StatusUpdatedEventDTO),
				////typeof(MinipoolVacancyPreparedEventDTO),
				////typeof(MinipoolPromotedEventDTO),
				////typeof(MinipoolDestroyedEventDTO), // Process?
				typeof(EtherWithdrawalProcessedEventDTO), // Exit
			], [], Policy)).ToList();

		foreach (IEventLog eventLog in validatorEvents)
		{
			await eventLog.WhenIsAsync<MinipoolPrestakedEventDTO>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MinipoolEnqueuedEventDTO>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MinipoolDequeuedEventDTO>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<EtherWithdrawalProcessedEventDTO>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<StatusUpdatedEventDTO>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorEnqueuedEventDTO>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorAssignedEventDTO>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorDequeuedEventDTO>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);
		}
	}

	protected override async Task<NodesSyncContext> LoadContextAsync(
		Web3 web3, RocketStorageService rocketStorage, ReadOnlyDictionary<string, RocketPoolContract> contracts,
		CancellationToken cancellationToken = default)
	{
		long activationHeight = contracts["rocketStorage"].Versions.Single().ActivationHeight;

		Logger.LogInformation("Loading {snapshot}", Keys.NodesSnapshot);
		BlobObject<NodesSnapshot> nodesSnapshot =
			await Storage.ReadAsync<NodesSnapshot>(Keys.NodesSnapshot, cancellationToken) ??
			new BlobObject<NodesSnapshot>
			{
				ProcessedBlockNumber = activationHeight,
				Data = new NodesSnapshot
				{
					Index = [],
					DailyRegistrations = [],
					TotalNodeCount = [],
				},
			};

		Logger.LogInformation("Loading {snapshot}", Keys.ValidatorSnapshot);
		BlobObject<ValidatorSnapshot> validatorSnapshot =
			await Storage.ReadAsync<ValidatorSnapshot>(Keys.ValidatorSnapshot, cancellationToken) ??
			new BlobObject<ValidatorSnapshot>
			{
				ProcessedBlockNumber = activationHeight,
				Data = new ValidatorSnapshot
				{
					MinipoolValidatorIndex = [],
					MegapoolValidatorIndex = [],
				},
			};

		Logger.LogInformation("Loading {snapshot}", Keys.QueueSnapshot);
		BlobObject<QueueSnapshot> queueSnapshot =
			await Storage.ReadAsync<QueueSnapshot>(Keys.QueueSnapshot, cancellationToken) ??
			new BlobObject<QueueSnapshot>
			{
				ProcessedBlockNumber = activationHeight,
				Data = new QueueSnapshot
				{
					StandardIndex = [],
					ExpressIndex = [],
					TotalQueueCount = [],
					DailyEnqueued = [],
					DailyDequeued = [],
					DailyVoluntaryExits = [],
				},
			};

		return new NodesSyncContext
		{
			Storage = Storage,
			Policy = Policy,
			Logger = this.loggerFactory.CreateLogger<NodesSyncContext>(),
			Web3 = web3,
			CurrentBlockHeight = nodesSnapshot.ProcessedBlockNumber,
			RocketStorage = rocketStorage,
			Contracts = contracts,
			RocketNodeManager =
				new RocketNodeManagerService(
					web3, await Policy.ExecuteAsync(() => rocketStorage.GetAddressQueryAsync("rocketNodeManager"))),
			RocketMinipoolManager =
				new RocketMinipoolManagerService(
					web3, await Policy.ExecuteAsync(() => rocketStorage.GetAddressQueryAsync("rocketMinipoolManager"))),
			RocketNodeManagerAddresses = contracts["rocketNodeManager"].Versions.Select(x => x.Address).ToArray(),
			Nodes = new NodeInfo
			{
				Data = new NodeInfo.NodeInfoFull
				{
					Index = new OrderedDictionary<string, NodeIndexEntry>(
						nodesSnapshot.Data.Index.Select(x =>
							new KeyValuePair<string, NodeIndexEntry>(x.ContractAddress.ToHex(true), x)),
						StringComparer.OrdinalIgnoreCase),
					TotalNodesCount = nodesSnapshot.Data.TotalNodeCount,
					DailyRegistrations = nodesSnapshot.Data.DailyRegistrations,
				},
			},
			ValidatorInfo = new ValidatorInfo
			{
				RocketMinipoolManagerAddresses =
					contracts["rocketMinipoolManager"].Versions.Select(x => x.Address).ToArray(),
				Data = new ValidatorInfo.ValidatorInfoFull
				{
					MinipoolValidatorIndex = new OrderedDictionary<string, MinipoolValidatorIndexEntry>(
						validatorSnapshot.Data.MinipoolValidatorIndex.Select(x =>
							new KeyValuePair<string, MinipoolValidatorIndexEntry>(x.MinipoolAddress.ToHex(true), x)),
						StringComparer.OrdinalIgnoreCase),
					MegapoolValidatorIndex =
						new OrderedDictionary<(string Address, int Index), MegapoolValidatorIndexEntry>(
							validatorSnapshot.Data.MegapoolValidatorIndex.Select(x =>
								new KeyValuePair<(string Address, int Index), MegapoolValidatorIndexEntry>(
									(x.MegapoolAddress.ToHex(true), x.MegapoolIndex), x)),
							new MegapoolIndexEqualityComparer()),
				},
			},
			QueueInfo = new QueueInfo
			{
				StandardQueue = queueSnapshot.Data.StandardIndex.ToList(),
				ExpressQueue = queueSnapshot.Data.ExpressIndex.ToList(),
				TotalQueueCount = new SortedList<DateOnly, int>(queueSnapshot.Data.TotalQueueCount),
				DailyEnqueued = queueSnapshot.Data.DailyEnqueued,
				DailyDequeued = queueSnapshot.Data.DailyDequeued,
				DailyVoluntaryExits = queueSnapshot.Data.DailyVoluntaryExits,
			},
		};
	}

	protected override async Task SaveContextAsync(
		NodesSyncContext context, CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Writing {snapshot}", Keys.NodesSnapshot);
		await Storage.WriteAsync(
			Keys.NodesSnapshot,
			new BlobObject<NodesSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new NodesSnapshot
				{
					Index = context.Nodes.Data.Index.Values.ToArray(),
					DailyRegistrations = context.Nodes.Data.DailyRegistrations,
					TotalNodeCount = context.Nodes.Data.TotalNodesCount,
				},
			}, cancellationToken: cancellationToken);

		Logger.LogInformation("Writing {snapshot}", Keys.QueueSnapshot);
		await Storage.WriteAsync(
			Keys.QueueSnapshot,
			new BlobObject<QueueSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new QueueSnapshot
				{
					TotalQueueCount = context.QueueInfo.TotalQueueCount,
					DailyEnqueued = context.QueueInfo.DailyEnqueued,
					DailyDequeued = context.QueueInfo.DailyDequeued,
					DailyVoluntaryExits = context.QueueInfo.DailyVoluntaryExits,
					StandardIndex = context.QueueInfo.StandardQueue.ToArray(),
					ExpressIndex = context.QueueInfo.ExpressQueue.ToArray(),
				},
			}, cancellationToken: cancellationToken);

		Logger.LogInformation("Writing {snapshot}", Keys.ValidatorSnapshot);
		await Storage.WriteAsync(
			Keys.ValidatorSnapshot,
			new BlobObject<ValidatorSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new ValidatorSnapshot
				{
					MinipoolValidatorIndex = context.ValidatorInfo.Data.MinipoolValidatorIndex.Values.ToArray(),
					MegapoolValidatorIndex = context.ValidatorInfo.Data.MegapoolValidatorIndex.Values.ToArray(),
				},
			}, cancellationToken: cancellationToken);

		await Parallel.ForEachAsync(
			context.Nodes.Partial.Updated.Values, cancellationToken, async (node, innerCancellationToken) =>
			{
				Logger.LogInformation("Writing {snapshot}", Keys.Node(node.ContractAddress.ToHex(true)));
				await Storage.WriteAsync(
					Keys.Node(node.ContractAddress.ToHex(true)), new BlobObject<Node>
					{
						ProcessedBlockNumber = context.CurrentBlockHeight,
						Data = node,
					}, cancellationToken: innerCancellationToken);
			});

		await Parallel.ForEachAsync(
			context.ValidatorInfo.Partial.UpdatedMegapoolValidators.Select(megapool =>
				(megapool.Key.Address, megapool.Key.Index, megapool.Value)),
			cancellationToken, async (validatorEntry, innerCancellationToken) =>
			{
				(string megapoolAddress, int megapoolIndex, Validator validator) = validatorEntry;

				Logger.LogInformation("Writing {snapshot}", Keys.MegapoolValidator(megapoolAddress, megapoolIndex));
				await Storage.WriteAsync(
					Keys.MegapoolValidator(megapoolAddress, megapoolIndex), new BlobObject<Validator>
					{
						ProcessedBlockNumber = context.CurrentBlockHeight,
						Data = validator,
					}, cancellationToken: innerCancellationToken);
			});

		await Parallel.ForEachAsync(
			context.ValidatorInfo.Partial.UpdatedMinipoolValidators, cancellationToken,
			async (validatorEntry, innerCancellationToken) =>
			{
				(string minipoolAddress, Validator validator) = validatorEntry;

				Logger.LogInformation("Writing {snapshot}", Keys.MinipoolValidator(minipoolAddress));
				await Storage.WriteAsync(
					Keys.MinipoolValidator(minipoolAddress), new BlobObject<Validator>
					{
						ProcessedBlockNumber = context.CurrentBlockHeight,
						Data = validator,
					}, cancellationToken: innerCancellationToken);
			});
	}
}