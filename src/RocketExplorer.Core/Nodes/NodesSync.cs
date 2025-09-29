using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using RocketExplorer.Core.Nodes.EventHandlers;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketMegapoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolManager;
using RocketExplorer.Ethereum.RocketMinipoolManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolQueue.ContractDefinition;
using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;
using Validator = RocketExplorer.Shared.Validators.Validator;

namespace RocketExplorer.Core.Nodes;

public class NodesSync(IOptions<SyncOptions> options)
	: SyncBase<NodesSyncContext>(options)
{
	protected override async Task HandleBlocksAsync(
		NodesSyncContext context, long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		IEnumerable<IEventLog> nodeAddedEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(NodeRegisteredEventDTO),],
			context.RocketNodeManagerAddresses, context.Policy);

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			await eventLog.WhenIsAsync<NodeRegisteredEventDTO, NodesSyncContext>(
				NodeRegisteredEventHandler.HandleAsync, context, cancellationToken);
		}

		IEnumerable<IEventLog> preSaturn1StakingEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [
				typeof(RPLLegacyStakedEventDto),
				typeof(RPLOrRPLLegacyWithdrawnEventDTO),
			],
			context.PreSaturn1RocketNodeStakingAddresses, context.Policy);

		foreach (IEventLog eventLog in preSaturn1StakingEvents)
		{
			await eventLog.WhenIsAsync<RPLLegacyStakedEventDto, NodesSyncContext>(
				StakingEventHandlers.HandleRPLLegacyStakedAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<RPLOrRPLLegacyWithdrawnEventDTO, NodesSyncContext>(
				StakingEventHandlers.HandleRPLLegacyUnstakedAsync, context, cancellationToken);
		}

		IEnumerable<IEventLog> postSaturn1StakingEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [
				typeof(RPLLegacyWithdrawnEventDTO),
				typeof(RPLStakedEventDTO),
				typeof(RPLUnstakedEventDTO),
			],
			context.PostSaturn1RocketNodeStakingAddresses, context.Policy);

		foreach (IEventLog eventLog in postSaturn1StakingEvents)
		{
			await eventLog.WhenIsAsync<RPLLegacyWithdrawnEventDTO, NodesSyncContext>(
				StakingEventHandlers.HandleRPLLegacyUnstakedAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<RPLStakedEventDTO, NodesSyncContext>(
				StakingEventHandlers.HandleRPLMegapoolStakedAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<RPLUnstakedEventDTO, NodesSyncContext>(
				StakingEventHandlers.HandleRPLMegapoolUnstakedAsync, context, cancellationToken);
		}

		IEnumerable<IEventLog> minipoolCreatedEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(MinipoolCreatedEventDTO),],
			context.ValidatorInfo.RocketMinipoolManagerAddresses, context.Policy);

		foreach (IEventLog eventLog in minipoolCreatedEvents)
		{
			await eventLog.WhenIsAsync<MinipoolCreatedEventDTO, NodesSyncContext>(
				MinipoolCreatedEventHandler.HandleAsync, context, cancellationToken);
		}

		List<IEventLog> validatorEvents = (await context.Web3.FilterAsync(
			fromBlock, toBlock,
			[
				typeof(MegapoolValidatorEnqueuedEventDTO),
				typeof(MegapoolValidatorDequeuedEventDTO),
				typeof(MegapoolValidatorAssignedEventDTO),
				typeof(MegapoolValidatorDissolvedEventDTO),
				typeof(MegapoolValidatorStakedEventDTO),
				typeof(MegapoolValidatorExitingEventDTO),
				typeof(MegapoolValidatorExitedEventDTO),
				typeof(MinipoolPrestakedEventDTO),
				typeof(MinipoolEnqueuedEventDTO),
				typeof(MinipoolDequeuedEventDTO),
				typeof(StatusUpdatedEventDTO),
				////typeof(MinipoolVacancyPreparedEventDTO),
				////typeof(MinipoolPromotedEventDTO),
				////typeof(MinipoolDestroyedEventDTO),
				typeof(EtherWithdrawalProcessedEventDTO), // Exit
			], [], context.Policy)).ToList();

		foreach (IEventLog eventLog in validatorEvents)
		{
			await eventLog.WhenIsAsync<MinipoolPrestakedEventDTO, NodesSyncContext>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MinipoolEnqueuedEventDTO, NodesSyncContext>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MinipoolDequeuedEventDTO, NodesSyncContext>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<EtherWithdrawalProcessedEventDTO, NodesSyncContext>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<StatusUpdatedEventDTO, NodesSyncContext>(
				MinipoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorEnqueuedEventDTO, NodesSyncContext>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorDequeuedEventDTO, NodesSyncContext>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorAssignedEventDTO, NodesSyncContext>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorDissolvedEventDTO, NodesSyncContext>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorStakedEventDTO, NodesSyncContext>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorExitingEventDTO, NodesSyncContext>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorExitedEventDTO, NodesSyncContext>(
				MegapoolEventHandlers.HandleAsync, context, cancellationToken);
		}
	}

	protected override async Task<NodesSyncContext> LoadContextAsync(
		ContextBase contextBase,
		CancellationToken cancellationToken = default)
	{
		long activationHeight = contextBase.Contracts["rocketStorage"].Versions.Single().ActivationHeight;

		contextBase.Logger.LogInformation("Loading {snapshot}", Keys.NodesSnapshot);
		BlobObject<NodesSnapshot> nodesSnapshot =
			await contextBase.Storage.ReadAsync<NodesSnapshot>(Keys.NodesSnapshot, cancellationToken) ??
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

		contextBase.Logger.LogInformation("Loading {snapshot}", Keys.ValidatorSnapshot);
		BlobObject<ValidatorSnapshot> validatorSnapshot =
			await contextBase.Storage.ReadAsync<ValidatorSnapshot>(Keys.ValidatorSnapshot, cancellationToken) ??
			new BlobObject<ValidatorSnapshot>
			{
				ProcessedBlockNumber = activationHeight,
				Data = new ValidatorSnapshot
				{
					MinipoolValidatorIndex = [],
					MegapoolValidatorIndex = [],
				},
			};

		contextBase.Logger.LogInformation("Loading {snapshot}", Keys.QueueSnapshot);
		BlobObject<QueueSnapshot> queueSnapshot =
			await contextBase.Storage.ReadAsync<QueueSnapshot>(Keys.QueueSnapshot, cancellationToken) ??
			new BlobObject<QueueSnapshot>
			{
				ProcessedBlockNumber = activationHeight,
				Data = new QueueSnapshot
				{
					MinipoolHalfQueue = [],
					MinipoolFullQueue = [],
					MinipoolVariableQueue = [],
					MegapoolQueueIndex = 0,
					MegapoolStandardQueue = [],
					MegapoolExpressQueue = [],
					TotalQueueCount = [],
					DailyEnqueued = [],
					DailyDequeued = [],
					DailyVoluntaryExits = [],
				},
			};

		return new NodesSyncContext
		{
			Storage = contextBase.Storage,
			Policy = contextBase.Policy,
			Logger = contextBase.Logger,
			Web3 = contextBase.Web3,
			BeaconChainService = contextBase.BeaconChainService,
			GlobalIndexService = contextBase.GlobalIndexService,
			DashboardInfo = contextBase.DashboardInfo,
			RocketStorage = contextBase.RocketStorage,
			Contracts = contextBase.Contracts,
			LatestBlockHeight = contextBase.LatestBlockHeight,

			CurrentBlockHeight = nodesSnapshot.ProcessedBlockNumber,

			RocketNodeManager =
				new RocketNodeManagerService(
					contextBase.Web3,
					await contextBase.Policy.ExecuteAsync(() =>
						contextBase.RocketStorage.GetAddressQueryAsync("rocketNodeManager"))),
			RocketMinipoolManager =
				new RocketMinipoolManagerService(
					contextBase.Web3,
					await contextBase.Policy.ExecuteAsync(() =>
						contextBase.RocketStorage.GetAddressQueryAsync("rocketMinipoolManager"))),
			RocketNodeManagerAddresses = contextBase.Contracts["rocketNodeManager"]
				.Versions.Select(x => x.Address)
				.ToArray(),
			PreSaturn1RocketNodeStakingAddresses = contextBase.Contracts["rocketNodeStaking"]
				.Versions.Where(x => x.Version <= 6)
				.Select(x => x.Address)
				.Concat(
				[
					AddressUtil.ZERO_ADDRESS,
				])
				.ToArray(),
			PostSaturn1RocketNodeStakingAddresses = contextBase.Contracts["rocketNodeStaking"]
				.Versions.Where(x => x.Version > 6)
				.Select(x => x.Address)
				.Concat(
				[
					AddressUtil.ZERO_ADDRESS,
				])
				.ToArray(),
			Nodes = new NodeInfo
			{
				Data = new NodeInfo.NodeInfoFull
				{
					Index = new OrderedDictionary<string, NodeIndexEntry>(
						nodesSnapshot.Data.Index.Select(x =>
							new KeyValuePair<string, NodeIndexEntry>(
								x.ContractAddress.ToHex(true),
								x)),
						StringComparer.OrdinalIgnoreCase),
					TotalNodesCount = nodesSnapshot.Data.TotalNodeCount,
					DailyRegistrations = nodesSnapshot.Data.DailyRegistrations,
				},
			},
			ValidatorInfo = new ValidatorInfo
			{
				RocketMinipoolManagerAddresses =
					contextBase.Contracts["rocketMinipoolManager"]
						.Versions.Select(x => x.Address)
						.ToArray(),
				Data = new ValidatorInfo.ValidatorInfoFull
				{
					MinipoolValidatorIndex = new OrderedDictionary<string, MinipoolValidatorIndexEntry>(
						validatorSnapshot.Data.MinipoolValidatorIndex.Select(x =>
							new KeyValuePair<string, MinipoolValidatorIndexEntry>(
								x.MinipoolAddress.ToHex(true),
								x)),
						StringComparer.OrdinalIgnoreCase),
					MegapoolValidatorIndex =
						new OrderedDictionary<(string Address, int Index), MegapoolValidatorIndexEntry>(
							validatorSnapshot.Data.MegapoolValidatorIndex.Select(x =>
								new KeyValuePair<(string Address, int Index), MegapoolValidatorIndexEntry>(
									(x.MegapoolAddress.ToHex(true), x.MegapoolIndex),
									x)),
							new MegapoolIndexEqualityComparer()),
				},
			},
			QueueInfo = new QueueInfo
			{
				MinipoolHalfQueue = queueSnapshot.Data.MinipoolHalfQueue.ToList(),
				MinipoolFullQueue = queueSnapshot.Data.MinipoolFullQueue.ToList(),
				MinipoolVariableQueue = queueSnapshot.Data.MinipoolVariableQueue.ToList(),
				MegapoolQueueIndex = queueSnapshot.Data.MegapoolQueueIndex,
				MegapoolStandardQueue = queueSnapshot.Data.MegapoolStandardQueue.ToList(),
				MegapoolExpressQueue = queueSnapshot.Data.MegapoolExpressQueue.ToList(),
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
		context.Logger.LogInformation("Writing {snapshot}", Keys.NodesSnapshot);
		await context.Storage.WriteAsync(
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

		context.Logger.LogInformation("Writing {snapshot}", Keys.QueueSnapshot);
		await context.Storage.WriteAsync(
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
					MinipoolHalfQueue = context.QueueInfo.MinipoolHalfQueue.ToArray(),
					MinipoolFullQueue = context.QueueInfo.MinipoolFullQueue.ToArray(),
					MinipoolVariableQueue = context.QueueInfo.MinipoolVariableQueue.ToArray(),
					MegapoolQueueIndex = context.QueueInfo.MegapoolQueueIndex,
					MegapoolStandardQueue = context.QueueInfo.MegapoolStandardQueue.ToArray(),
					MegapoolExpressQueue = context.QueueInfo.MegapoolExpressQueue.ToArray(),
				},
			}, cancellationToken: cancellationToken);

		context.Logger.LogInformation("Writing {snapshot}", Keys.ValidatorSnapshot);
		await context.Storage.WriteAsync(
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
				context.Logger.LogInformation("Writing {snapshot}", Keys.Node(node.ContractAddress.ToHex(true)));
				await context.Storage.WriteAsync(
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

				context.Logger.LogInformation(
					"Writing {snapshot}", Keys.MegapoolValidator(megapoolAddress, megapoolIndex));
				await context.Storage.WriteAsync(
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

				context.Logger.LogInformation("Writing {snapshot}", Keys.MinipoolValidator(minipoolAddress));
				await context.Storage.WriteAsync(
					Keys.MinipoolValidator(minipoolAddress), new BlobObject<Validator>
					{
						ProcessedBlockNumber = context.CurrentBlockHeight,
						Data = validator,
					}, cancellationToken: innerCancellationToken);
			});
	}
}