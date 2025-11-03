using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public record class NodesContext
{
	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public required NodeInfo Nodes { get; init; }

	public required string[] PostSaturn1RocketNodeStakingAddresses { get; init; }

	public required string[] PreSaturn1RocketNodeStakingAddresses { get; init; }

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required QueueInfo QueueInfo { get; init; }

	public required string[] RocketNodeManagerAddresses { get; set; }

	public required ValidatorInfo ValidatorInfo { get; init; }

	public static async Task<NodesContext> ReadAsync(
		Storage storage, ContractsContext contractsContext, ILogger<NodesContext> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading {snapshot}", Keys.NodesSnapshot);
		Task<BlobObject<NodesSnapshot>?> readNodesSnapshotTask =
			storage.ReadAsync<NodesSnapshot>(Keys.NodesSnapshot, cancellationToken);

		logger.LogInformation("Loading {snapshot}", Keys.NodesExtendedSnapshot);
		Task<BlobObject<NodesExtendedSnapshot>?> readNodesExtendedSnapshotTask =
			storage.ReadAsync<NodesExtendedSnapshot>(Keys.NodesExtendedSnapshot, cancellationToken);

		logger.LogInformation("Loading {snapshot}", Keys.ValidatorSnapshot);
		Task<BlobObject<ValidatorSnapshot>?> readValidatorSnapshotTask =
			storage.ReadAsync<ValidatorSnapshot>(Keys.ValidatorSnapshot, cancellationToken);

		logger.LogInformation("Loading {snapshot}", Keys.QueueSnapshot);
		Task<BlobObject<QueueSnapshot>?> readQueueSnapshotTask =
			storage.ReadAsync<QueueSnapshot>(Keys.QueueSnapshot, cancellationToken);

		await Task.WhenAll(readNodesSnapshotTask, readValidatorSnapshotTask, readQueueSnapshotTask);

		await contractsContext.IsFinished;

		ReadOnlyDictionary<string, RocketPoolContract> contracts = contractsContext.ContextContracts.AsReadOnly();
		long activationHeight = contracts["rocketStorage"].Versions.Single().ActivationHeight;

		BlobObject<NodesSnapshot> nodesSnapshot =
			await readNodesSnapshotTask ??
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

		BlobObject<NodesExtendedSnapshot> nodesExtendedSnapshot =
			await readNodesExtendedSnapshotTask ??
			new BlobObject<NodesExtendedSnapshot>
			{
				ProcessedBlockNumber = activationHeight,
				Data = new NodesExtendedSnapshot
				{
					WithdrawalAddresses = [],
					RPLWithdrawalAddresses = [],
					StakeOnBehalfAddresses = [],
				},
			};

		BlobObject<ValidatorSnapshot> validatorSnapshot =
			await readValidatorSnapshotTask ??
			new BlobObject<ValidatorSnapshot>
			{
				ProcessedBlockNumber = activationHeight,
				Data = new ValidatorSnapshot
				{
					MinipoolValidatorIndex = [],
					MegapoolValidatorIndex = [],
				},
			};

		BlobObject<QueueSnapshot> queueSnapshot =
			await readQueueSnapshotTask ??
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

		return new NodesContext
		{
			CurrentBlockHeight = nodesSnapshot.ProcessedBlockNumber,
			RocketNodeManagerAddresses = contracts["rocketNodeManager"]
				.Versions.Select(x => x.Address)
				.ToArray(),
			PreSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"]
				.Versions.Where(x => x.Version <= 6)
				.Select(x => x.Address)
				.Concat(
				[
					AddressUtil.ZERO_ADDRESS,
				])
				.ToArray(),
			PostSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"]
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
					WithdrawalAddresses = nodesExtendedSnapshot.Data.WithdrawalAddresses.ToDictionary(pair => pair.Key.ToHex(true), pair => pair.Value?.ToHex(true)),
					RPLWithdrawalAddresses = nodesExtendedSnapshot.Data.RPLWithdrawalAddresses.ToDictionary(pair => pair.Key.ToHex(true), pair => pair.Value?.ToHex(true)),
					StakeOnBehalfAddresses = nodesExtendedSnapshot.Data.StakeOnBehalfAddresses.ToDictionary(pair => pair.Key.ToHex(true), pair => pair.Value?.Select(y => y.ToHex(true)).ToList() ?? []),
				},
			},
			ValidatorInfo = new ValidatorInfo
			{
				RocketMinipoolManagerAddresses = contracts["rocketMinipoolManager"]
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

	public async Task SaveAsync(
		Storage storage, ILogger<NodesContext> logger, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.NodesSnapshot);
		Task writeNodesTask = storage.WriteAsync(
			Keys.NodesSnapshot,
			new BlobObject<NodesSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new NodesSnapshot
				{
					Index = Nodes.Data.Index.Values.ToArray(),
					DailyRegistrations = Nodes.Data.DailyRegistrations,
					TotalNodeCount = Nodes.Data.TotalNodesCount,
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.QueueSnapshot);
		Task writeQueueTask = storage.WriteAsync(
			Keys.QueueSnapshot,
			new BlobObject<QueueSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new QueueSnapshot
				{
					TotalQueueCount = QueueInfo.TotalQueueCount,
					DailyEnqueued = QueueInfo.DailyEnqueued,
					DailyDequeued = QueueInfo.DailyDequeued,
					DailyVoluntaryExits = QueueInfo.DailyVoluntaryExits,
					MinipoolHalfQueue = QueueInfo.MinipoolHalfQueue.ToArray(),
					MinipoolFullQueue = QueueInfo.MinipoolFullQueue.ToArray(),
					MinipoolVariableQueue = QueueInfo.MinipoolVariableQueue.ToArray(),
					MegapoolQueueIndex = QueueInfo.MegapoolQueueIndex,
					MegapoolStandardQueue = QueueInfo.MegapoolStandardQueue.ToArray(),
					MegapoolExpressQueue = QueueInfo.MegapoolExpressQueue.ToArray(),
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.ValidatorSnapshot);
		Task writeValidatorTask = storage.WriteAsync(
			Keys.ValidatorSnapshot,
			new BlobObject<ValidatorSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new ValidatorSnapshot
				{
					MinipoolValidatorIndex = ValidatorInfo.Data.MinipoolValidatorIndex.Values.ToArray(),
					MegapoolValidatorIndex = ValidatorInfo.Data.MegapoolValidatorIndex.Values.ToArray(),
				},
			}, cancellationToken: cancellationToken);

		await Task.WhenAll(writeNodesTask, writeQueueTask, writeValidatorTask);

		await Parallel.ForEachAsync(
			Nodes.Partial.Updated.Values, cancellationToken, async (node, innerCancellationToken) =>
			{
				logger.LogInformation("Writing {snapshot}", Keys.Node(node.ContractAddress.ToHex(true)));
				await storage.WriteAsync(
					Keys.Node(node.ContractAddress.ToHex(true)), new BlobObject<Node>
					{
						ProcessedBlockNumber = CurrentBlockHeight,
						Data = node,
					}, cancellationToken: innerCancellationToken);
			});

		await Parallel.ForEachAsync(
			ValidatorInfo.Partial.UpdatedMegapoolValidators.Select(megapool =>
				(megapool.Key.Address, megapool.Key.Index, megapool.Value)),
			cancellationToken, async (validatorEntry, innerCancellationToken) =>
			{
				(string megapoolAddress, int megapoolIndex, Validator validator) = validatorEntry;

				logger.LogInformation("Writing {snapshot}", Keys.MegapoolValidator(megapoolAddress, megapoolIndex));
				await storage.WriteAsync(
					Keys.MegapoolValidator(megapoolAddress, megapoolIndex), new BlobObject<Validator>
					{
						ProcessedBlockNumber = CurrentBlockHeight,
						Data = validator,
					}, cancellationToken: innerCancellationToken);
			});

		await Parallel.ForEachAsync(
			ValidatorInfo.Partial.UpdatedMinipoolValidators, cancellationToken,
			async (validatorEntry, innerCancellationToken) =>
			{
				(string minipoolAddress, Validator validator) = validatorEntry;

				logger.LogInformation("Writing {snapshot}", Keys.MinipoolValidator(minipoolAddress));
				await storage.WriteAsync(
					Keys.MinipoolValidator(minipoolAddress), new BlobObject<Validator>
					{
						ProcessedBlockNumber = CurrentBlockHeight,
						Data = validator,
					}, cancellationToken: innerCancellationToken);
			});
	}
}