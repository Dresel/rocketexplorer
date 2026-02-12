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

public record class NodesMasterContext
{
	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public required NodesMasterState Nodes { get; init; }

	public required string[] PostSaturn1RocketNodeStakingAddresses { get; init; }

	public required string[] PreSaturn1RocketNodeStakingAddresses { get; init; }

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required QueueInfo QueueInfo { get; init; }

	public required string[] RocketNodeManagerAddresses { get; set; }

	public required string[] RocketMinipoolManagerAddresses { get; init; }

	public static async Task<NodesMasterContext> ReadAsync(
		Storage storage, ContractsContext contractsContext, ILogger<NodesMasterContext> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading {snapshot}", Keys.NodesMasterSnapshot);
		Task<BlobObject<NodesMasterSnapshot>?> readMasterSnapshotTask =
			storage.ReadAsync<NodesMasterSnapshot>(Keys.NodesMasterSnapshot, cancellationToken);

		await contractsContext.IsFinished;

		ReadOnlyDictionary<string, RocketPoolContract> contracts = contractsContext.ContextContracts.AsReadOnly();
		long activationHeight = contracts["rocketStorage"].Versions.Single().ActivationHeight;

		BlobObject<NodesMasterSnapshot> masterSnapshot =
			await readMasterSnapshotTask ??
			new BlobObject<NodesMasterSnapshot>
			{
				ProcessedBlockNumber = activationHeight,
				Data = new NodesMasterSnapshot
				{
					Nodes = [],
					DailyRegistrations = [],
					TotalNodeCount = [],
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

		var nodes = new OrderedDictionary<string, NodeMasterInfo>(
			masterSnapshot.Data.Nodes.Select(x =>
				new KeyValuePair<string, NodeMasterInfo>(
					x.ContractAddress.ToHex(true),
					FromNodeMaster(x))),
			StringComparer.OrdinalIgnoreCase);

		var minipoolNodeAddresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var megapoolNodeAddresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach ((string nodeAddress, NodeMasterInfo node) in nodes)
		{
			foreach (string minipoolAddress in node.MinipoolValidators.Keys)
			{
				minipoolNodeAddresses[minipoolAddress] = nodeAddress;
			}

			if (node.MegapoolAddress is not null)
			{
				megapoolNodeAddresses[node.MegapoolAddress.ToHex(true)] = nodeAddress;
			}
		}

		return new NodesMasterContext
		{
			CurrentBlockHeight = masterSnapshot.ProcessedBlockNumber,
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
			RocketMinipoolManagerAddresses = contracts["rocketMinipoolManager"]
				.Versions.Select(x => x.Address)
				.ToArray(),
			Nodes = new NodesMasterState
			{
				Data = new NodesMasterState.NodesMasterStateFull
				{
					Nodes = nodes,
					DailyRegistrations = masterSnapshot.Data.DailyRegistrations,
					TotalNodesCount = masterSnapshot.Data.TotalNodeCount,
					MinipoolNodeAddresses = minipoolNodeAddresses,
					MegapoolNodeAddresses = megapoolNodeAddresses,
				},
			},
			QueueInfo = new QueueInfo
			{
				MinipoolHalfQueue = masterSnapshot.Data.MinipoolHalfQueue.ToList(),
				MinipoolFullQueue = masterSnapshot.Data.MinipoolFullQueue.ToList(),
				MinipoolVariableQueue = masterSnapshot.Data.MinipoolVariableQueue.ToList(),
				MegapoolQueueIndex = masterSnapshot.Data.MegapoolQueueIndex,
				MegapoolStandardQueue = masterSnapshot.Data.MegapoolStandardQueue.ToList(),
				MegapoolExpressQueue = masterSnapshot.Data.MegapoolExpressQueue.ToList(),
				TotalQueueCount = new SortedList<DateOnly, int>(masterSnapshot.Data.TotalQueueCount),
				DailyEnqueued = masterSnapshot.Data.DailyEnqueued,
				DailyDequeued = masterSnapshot.Data.DailyDequeued,
				DailyVoluntaryExits = masterSnapshot.Data.DailyVoluntaryExits,
			},
		};
	}

	public async Task SaveAsync(
		Storage storage, ILogger<NodesMasterContext> logger, CancellationToken cancellationToken = default)
	{
		// Build derived snapshot data from master
		var nodeIndexEntries = new List<NodeIndexEntry>();
		var withdrawalAddresses = new Dictionary<byte[], byte[]>(new FastByteArrayComparer());
		var rplWithdrawalAddresses = new Dictionary<byte[], byte[]>(new FastByteArrayComparer());
		var stakeOnBehalfAddresses = new Dictionary<byte[], HashSet<byte[]>>(new FastByteArrayComparer());
		var minipoolValidatorIndex = new List<MinipoolValidatorIndexEntry>();
		var megapoolValidatorIndex = new List<MegapoolValidatorIndexEntry>();

		foreach (NodeMasterInfo node in Nodes.Data.Nodes.Values)
		{
			nodeIndexEntries.Add(ToNodeIndexEntry(node));

			if (node.WithdrawalAddress is not null)
			{
				withdrawalAddresses[node.ContractAddress] = node.WithdrawalAddress;
			}

			if (node.RPLWithdrawalAddress is not null)
			{
				rplWithdrawalAddresses[node.ContractAddress] = node.RPLWithdrawalAddress;
			}

			if (node.StakeOnBehalfAddresses.Count > 0)
			{
				stakeOnBehalfAddresses[node.ContractAddress] =
					new HashSet<byte[]>(node.StakeOnBehalfAddresses, new FastByteArrayComparer());
			}

			foreach (ValidatorMasterInfo v in node.MinipoolValidators.Values)
			{
				minipoolValidatorIndex.Add(ToMinipoolValidatorIndexEntry(node.ContractAddress, v));
			}

			foreach (ValidatorMasterInfo v in node.MegapoolValidators.Values)
			{
				megapoolValidatorIndex.Add(ToMegapoolValidatorIndexEntry(node.ContractAddress, v));
			}
		}

		// Write master snapshot
		logger.LogInformation("Writing {snapshot}", Keys.NodesMasterSnapshot);
		Task writeMasterSnapshotTask = storage.WriteAsync(
			Keys.NodesMasterSnapshot,
			new BlobObject<NodesMasterSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new NodesMasterSnapshot
				{
					Nodes = Nodes.Data.Nodes.Values.Select(ToNodeMaster).ToArray(),
					DailyRegistrations = Nodes.Data.DailyRegistrations,
					TotalNodeCount = Nodes.Data.TotalNodesCount,
					MinipoolHalfQueue = QueueInfo.MinipoolHalfQueue.ToArray(),
					MinipoolFullQueue = QueueInfo.MinipoolFullQueue.ToArray(),
					MinipoolVariableQueue = QueueInfo.MinipoolVariableQueue.ToArray(),
					MegapoolQueueIndex = QueueInfo.MegapoolQueueIndex,
					MegapoolStandardQueue = QueueInfo.MegapoolStandardQueue.ToArray(),
					MegapoolExpressQueue = QueueInfo.MegapoolExpressQueue.ToArray(),
					TotalQueueCount = QueueInfo.TotalQueueCount,
					DailyEnqueued = QueueInfo.DailyEnqueued,
					DailyDequeued = QueueInfo.DailyDequeued,
					DailyVoluntaryExits = QueueInfo.DailyVoluntaryExits,
				},
			}, cancellationToken: cancellationToken);

		// Write derived snapshots
		logger.LogInformation("Writing {snapshot}", Keys.NodesSnapshot);
		Task writeNodesSnapshotTask = storage.WriteAsync(
			Keys.NodesSnapshot,
			new BlobObject<NodesSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new NodesSnapshot
				{
					Index = nodeIndexEntries.ToArray(),
					DailyRegistrations = Nodes.Data.DailyRegistrations,
					TotalNodeCount = Nodes.Data.TotalNodesCount,
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.NodesExtendedSnapshot);
		Task writeNodesExtendedSnapshotTask = storage.WriteAsync(
			Keys.NodesExtendedSnapshot,
			new BlobObject<NodesExtendedSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new NodesExtendedSnapshot
				{
					WithdrawalAddresses = withdrawalAddresses,
					RPLWithdrawalAddresses = rplWithdrawalAddresses,
					StakeOnBehalfAddresses = stakeOnBehalfAddresses,
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.ValidatorSnapshot);
		Task writeValidatorSnapshotTask = storage.WriteAsync(
			Keys.ValidatorSnapshot,
			new BlobObject<ValidatorSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new ValidatorSnapshot
				{
					MinipoolValidatorIndex = minipoolValidatorIndex.ToArray(),
					MegapoolValidatorIndex = megapoolValidatorIndex.ToArray(),
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.QueueSnapshot);
		Task writeQueueSnapshotTask = storage.WriteAsync(
			Keys.QueueSnapshot,
			new BlobObject<QueueSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new QueueSnapshot
				{
					MinipoolHalfQueue = QueueInfo.MinipoolHalfQueue.ToArray(),
					MinipoolFullQueue = QueueInfo.MinipoolFullQueue.ToArray(),
					MinipoolVariableQueue = QueueInfo.MinipoolVariableQueue.ToArray(),
					MegapoolQueueIndex = QueueInfo.MegapoolQueueIndex,
					MegapoolStandardQueue = QueueInfo.MegapoolStandardQueue.ToArray(),
					MegapoolExpressQueue = QueueInfo.MegapoolExpressQueue.ToArray(),
					TotalQueueCount = QueueInfo.TotalQueueCount,
					DailyEnqueued = QueueInfo.DailyEnqueued,
					DailyDequeued = QueueInfo.DailyDequeued,
					DailyVoluntaryExits = QueueInfo.DailyVoluntaryExits,
				},
			}, cancellationToken: cancellationToken);

		await Task.WhenAll(
			writeMasterSnapshotTask,
			writeNodesSnapshotTask,
			writeNodesExtendedSnapshotTask,
			writeValidatorSnapshotTask,
			writeQueueSnapshotTask);

		// Write updated nodes individually
		await Parallel.ForEachAsync(
			Nodes.NodesUpdated, cancellationToken, async (nodeAddress, innerCancellationToken) =>
			{
				if (!Nodes.Data.Nodes.TryGetValue(nodeAddress, out NodeMasterInfo? node))
				{
					return;
				}

				logger.LogInformation("Writing {snapshot}", Keys.Node(nodeAddress));
				await storage.WriteAsync(
					Keys.Node(nodeAddress), new BlobObject<NodeMaster>
					{
						ProcessedBlockNumber = CurrentBlockHeight,
						Data = ToNodeMaster(node),
					}, cancellationToken: innerCancellationToken);
			});

		// Write updated megapool validators individually
		await Parallel.ForEachAsync(
			Nodes.MegapoolValidatorsUpdated, cancellationToken,
			async (entry, innerCancellationToken) =>
			{
				(string nodeAddress, string megapoolAddress, int megapoolIndex) = entry;

				if (!Nodes.Data.Nodes.TryGetValue(nodeAddress, out NodeMasterInfo? node))
				{
					return;
				}

				if (!node.MegapoolValidators.TryGetValue((megapoolAddress, megapoolIndex), out ValidatorMasterInfo? validator))
				{
					return;
				}

				logger.LogInformation("Writing {snapshot}", Keys.MegapoolValidator(megapoolAddress, megapoolIndex));
				await storage.WriteAsync(
					Keys.MegapoolValidator(megapoolAddress, megapoolIndex), new BlobObject<ValidatorMaster>
					{
						ProcessedBlockNumber = CurrentBlockHeight,
						Data = ToValidatorMaster(validator),
					}, cancellationToken: innerCancellationToken);
			});

		// Write updated minipool validators individually
		await Parallel.ForEachAsync(
			Nodes.MinipoolValidatorsUpdated, cancellationToken,
			async (entry, innerCancellationToken) =>
			{
				(string nodeAddress, string minipoolAddress) = entry;

				if (!Nodes.Data.Nodes.TryGetValue(nodeAddress, out NodeMasterInfo? node))
				{
					return;
				}

				if (!node.MinipoolValidators.TryGetValue(minipoolAddress, out ValidatorMasterInfo? validator))
				{
					return;
				}

				logger.LogInformation("Writing {snapshot}", Keys.MinipoolValidator(minipoolAddress));
				await storage.WriteAsync(
					Keys.MinipoolValidator(minipoolAddress), new BlobObject<ValidatorMaster>
					{
						ProcessedBlockNumber = CurrentBlockHeight,
						Data = ToValidatorMaster(validator),
					}, cancellationToken: innerCancellationToken);
			});
	}

	private static NodeMasterInfo FromNodeMaster(NodeMaster nodeMaster) => new()
	{
		ContractAddress = nodeMaster.ContractAddress,
		RegistrationTimestamp = nodeMaster.RegistrationTimestamp,
		MegapoolAddress = nodeMaster.MegapoolAddress,
		MinipoolValidators = nodeMaster.MinipoolValidators
			.Where(v => v.MinipoolAddress is not null)
			.ToDictionary(
				v => v.MinipoolAddress!.ToHex(true),
				FromValidatorMaster,
				StringComparer.OrdinalIgnoreCase),
		MegapoolValidators = nodeMaster.MegapoolValidators
			.Where(v => v.MegapoolAddress is not null && v.MegapoolIndex is not null)
			.ToDictionary(
				v => (v.MegapoolAddress!.ToHex(true), v.MegapoolIndex!.Value),
				FromValidatorMaster,
				new MegapoolIndexEqualityComparer()),
		Timezone = nodeMaster.Timezone,
		RPLLegacyStaked = nodeMaster.RPLLegacyStaked,
		RPLMegapoolStaked = nodeMaster.RPLMegapoolStaked,
		WithdrawalAddress = nodeMaster.WithdrawalAddress,
		RPLWithdrawalAddress = nodeMaster.RPLWithdrawalAddress,
		StakeOnBehalfAddresses = new HashSet<byte[]>(nodeMaster.StakeOnBehalfAddresses, new FastByteArrayComparer()),
		InSmoothingPool = nodeMaster.InSmoothingPool,
	};

	private static ValidatorMasterInfo FromValidatorMaster(ValidatorMaster validator) => new()
	{
		MegapoolAddress = validator.MegapoolAddress,
		MinipoolAddress = validator.MinipoolAddress,
		PubKey = validator.PubKey,
		ValidatorIndex = validator.ValidatorIndex,
		ExpressTicketUsed = validator.ExpressTicketUsed,
		MegapoolIndex = validator.MegapoolIndex,
		Type = validator.Type,
		Bond = validator.Bond,
		Status = validator.Status,
		History = [.. validator.History],
	};

	private static NodeMaster ToNodeMaster(NodeMasterInfo info) => new()
	{
		ContractAddress = info.ContractAddress,
		RegistrationTimestamp = info.RegistrationTimestamp,
		MegapoolAddress = info.MegapoolAddress,
		MinipoolValidators = info.MinipoolValidators.Values.Select(ToValidatorMaster).ToArray(),
		MegapoolValidators = info.MegapoolValidators.Values.Select(ToValidatorMaster).ToArray(),
		Timezone = info.Timezone,
		RPLLegacyStaked = info.RPLLegacyStaked,
		RPLMegapoolStaked = info.RPLMegapoolStaked,
		WithdrawalAddress = info.WithdrawalAddress,
		RPLWithdrawalAddress = info.RPLWithdrawalAddress,
		StakeOnBehalfAddresses = new HashSet<byte[]>(info.StakeOnBehalfAddresses, new FastByteArrayComparer()),
		InSmoothingPool = info.InSmoothingPool,
	};

	private static ValidatorMaster ToValidatorMaster(ValidatorMasterInfo info) => new()
	{
		MegapoolAddress = info.MegapoolAddress,
		MinipoolAddress = info.MinipoolAddress,
		PubKey = info.PubKey,
		ValidatorIndex = info.ValidatorIndex,
		ExpressTicketUsed = info.ExpressTicketUsed,
		MegapoolIndex = info.MegapoolIndex,
		Type = info.Type,
		Bond = info.Bond,
		Status = info.Status,
		History = info.History.ToArray(),
	};

	private static NodeIndexEntry ToNodeIndexEntry(NodeMasterInfo info) => new()
	{
		ContractAddress = info.ContractAddress,
		RegistrationTimestamp = info.RegistrationTimestamp,
		MegapoolAddress = info.MegapoolAddress,
	};

	private static MinipoolValidatorIndexEntry ToMinipoolValidatorIndexEntry(
		byte[] nodeAddress, ValidatorMasterInfo info) => new()
	{
		NodeAddress = nodeAddress,
		MinipoolAddress = info.MinipoolAddress!,
		PubKey = info.PubKey,
		ValidatorIndex = info.ValidatorIndex,
	};

	private static MegapoolValidatorIndexEntry ToMegapoolValidatorIndexEntry(
		byte[] nodeAddress, ValidatorMasterInfo info) => new()
	{
		NodeAddress = nodeAddress,
		MegapoolAddress = info.MegapoolAddress!,
		MegapoolIndex = info.MegapoolIndex!.Value,
		PubKey = info.PubKey!,
		ValidatorIndex = info.ValidatorIndex,
	};
}
