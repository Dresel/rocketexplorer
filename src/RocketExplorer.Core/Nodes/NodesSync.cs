using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketMegapoolDelegate;
using RocketExplorer.Ethereum.RocketMegapoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolDelegate;
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

		IEnumerable<IEventLog> minipoolCreatedEvents = await context.Web3.FilterAsync(
			(ulong)fromBlock, (ulong)toBlock, [typeof(MinipoolCreatedEventDTO),],
			context.ValidatorInfo.RocketMinipoolManagerAddresses, Policy);

		foreach (IEventLog eventLog in minipoolCreatedEvents)
		{
			await eventLog.WhenIsAsync<MinipoolCreatedEventDTO>(
				(@event, log, innerCancellationToken) => EventAddMinipoolValidatorAsync(
					context, @event, log, innerCancellationToken),
				cancellationToken);
		}

		List<IEventLog> validatorEvents = (await context.Web3.FilterAsync(
			(ulong)fromBlock, (ulong)toBlock,
			[
				typeof(MegapoolValidatorEnqueuedEventDTO),
				typeof(MegapoolValidatorAssignedEventDTO),
				typeof(MegapoolValidatorDequeuedEventDTO),
				typeof(MinipoolEnqueuedEventDTO),
				typeof(MinipoolDequeuedEventDTO),
				typeof(MinipoolPrestakedEventDTO),
				typeof(StatusUpdatedEventDTO),
				////typeof(MinipoolVacancyPreparedEventDTO),
				////typeof(MinipoolPromotedEventDTO),
				////typeof(MinipoolDestroyedEventDTO), // Process?
				typeof(EtherWithdrawalProcessedEventDTO), // Exit
			], [], Policy)).ToList();

		Logger.LogInformation("Processing {EventCount} validator events", validatorEvents.Count());

		foreach (IEventLog eventLog in validatorEvents)
		{
			await eventLog.WhenIsAsync<MinipoolPrestakedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMinipoolValidatorAsync(
					context, log.Address.ConvertToEthereumChecksumAddress(), ValidatorStatus.PreStaked,
					@event.ValidatorPubkey, @event.Time, log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<MinipoolEnqueuedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMinipoolValidatorAsync(
					context, @event.Minipool, ValidatorStatus.InQueue, null, @event.Time,
					log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<MinipoolDequeuedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMinipoolValidatorAsync(
					context, @event.Minipool, ValidatorStatus.Dequeued, null,
					@event.Time, log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<EtherWithdrawalProcessedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMinipoolValidatorAsync(
					context, log.Address.ConvertToEthereumChecksumAddress(), ValidatorStatus.Exited, null, @event.Time,
					log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<StatusUpdatedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMinipoolValidatorAsync(
					context, log.Address.ConvertToEthereumChecksumAddress(), @event.Status.ToValidatorStatus(), null,
					@event.Time, log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorEnqueuedEventDTO>(
				(@event, log, innerCancellationToken) => EventAddNewMegapoolValidator(
					context, @event, log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorAssignedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMegapoolValidatorAsync(
					context, log.Address.ConvertToEthereumChecksumAddress(), (int)@event.ValidatorId,
					ValidatorStatus.Staking,
					@event.Time,
					log, innerCancellationToken), cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorDequeuedEventDTO>(
				(@event, log, innerCancellationToken) => EventUpdateMegapoolValidatorAsync(
					context, log.Address.ConvertToEthereumChecksumAddress(), (int)@event.ValidatorId,
					ValidatorStatus.Dequeued,
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
		BlobObject<NodesSnapshot> nodesSnapshot =

			//await Storage.ReadAsync<NodesSnapshot>(Keys.NodesSnapshot, cancellationToken) ??
			new()
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

			//await Storage.ReadAsync<QueueSnapshot>(Keys.QueueSnapshot, cancellationToken) ??
			new()
			{
				ProcessedBlockNumber = activationHeight,
				Data = new ValidatorSnapshot
				{
					Index = [],
				},
			};

		Logger.LogInformation("Loading {snapshot}", Keys.QueueSnapshot);
		BlobObject<QueueSnapshot> queueSnapshot =

			//await Storage.ReadAsync<QueueSnapshot>(Keys.QueueSnapshot, cancellationToken) ??
			new()
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
			NodeIndex =
				nodesSnapshot.Data.Index.ToDictionary(
					x => x.ContractAddress.ToHex(true), x => x, StringComparer.OrdinalIgnoreCase),
			TotalNodesCount = nodesSnapshot.Data.TotalNodeCount,
			DailyRegistrations = nodesSnapshot.Data.DailyRegistrations,
			ValidatorInfo = new ValidatorInfo
			{
				RocketMinipoolManagerAddresses =
					contracts["rocketMinipoolManager"].Versions.Select(x => x.Address).ToArray(),
				MinipoolValidatorIndex = validatorSnapshot.Data.Index.ToDictionary(
					x => x.MinipoolAddress?.ToHex(true) ?? x.MegapoolAddress.ToHex(true), x => x,
					StringComparer.OrdinalIgnoreCase),
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
					Index = context.NodeIndex.Values.ToArray(),
					DailyRegistrations = context.DailyRegistrations,
					TotalNodeCount = context.TotalNodesCount,
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

		// TODO: Save validator snapshot
		////Keys.ValidatorSnapshot

		foreach (Node node in context.Nodes.Values)
		{
			Logger.LogInformation("Writing {snapshot}", Keys.Node(node.ContractAddress.ToHex(true)));
			await Storage.WriteAsync(
				Keys.Node(node.ContractAddress.ToHex(true)), new BlobObject<Node>
				{
					ProcessedBlockNumber = context.CurrentBlockHeight,
					Data = node,
				}, cancellationToken: cancellationToken);
		}

		foreach ((string? megapoolAddress, int megapoolIndex, Validator? minipool) in
				context.ValidatorInfo.MegapoolValidators.SelectMany(megapool =>
					megapool.Value.Select(index => (megapool.Key, index.Key, index.Value))))
		{
			Logger.LogInformation("Writing {snapshot}", Keys.MegapoolValidator(megapoolAddress, megapoolIndex));
			await Storage.WriteAsync(
				Keys.MegapoolValidator(megapoolAddress, megapoolIndex), new BlobObject<Validator>
				{
					ProcessedBlockNumber = context.CurrentBlockHeight,
					Data = minipool,
				}, cancellationToken: cancellationToken);
		}

		await Parallel.ForEachAsync(
			context.ValidatorInfo.MinipoolValidators, cancellationToken,
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

	private async Task EventAddMinipoolValidatorAsync(
		NodesSyncContext context, MinipoolCreatedEventDTO @event, FilterLog log,
		CancellationToken cancellationToken = default)
	{
		RocketMinipoolDelegateService minipoolDelegate = new(context.Web3, @event.Minipool);
		string nodeOperatorAddress =
			await minipoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(log.BlockNumber));

		// This should not happen
		if (!context.NodeIndex.ContainsKey(nodeOperatorAddress))
		{
			logger.LogError(
				"Node operator {NodeOperatorAddress} for {Minipool} not found in index.", nodeOperatorAddress,
				@event.Minipool);
			return;
		}

		context.ValidatorInfo.MinipoolNodeOperatorMap[@event.Minipool] = nodeOperatorAddress;

		ValidatorIndexEntry entry = new()
		{
			NodeAddress = nodeOperatorAddress.HexToByteArray(),
			MinipoolAddress = @event.Minipool.HexToByteArray(),
			PubKey = null,
		};

		context.ValidatorInfo.MinipoolValidatorIndex[@event.Minipool] = entry;

		context.ValidatorInfo.MinipoolValidators[@event.Minipool] = new Validator
		{
			NodeAddress = entry.NodeAddress,
			MinipoolAddress = entry.MinipoolAddress,
			PubKey = entry.PubKey,
			Status = ValidatorStatus.Created,
			Bond = (float)UnitConversion.Convert.FromWei(
				await minipoolDelegate.GetNodeDepositBalanceQueryAsync(new BlockParameter(log.BlockNumber))),
			Type = ValidatorType.Legacy,
			History =
			[
				new ValidatorHistory
				{
					Status = ValidatorStatus.Created,
					Timestamp = (long)@event.Time,
				},
			],
		};

		// Update node
		if (!context.Nodes.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes[nodeOperatorAddress] =
				(await Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.Nodes[nodeOperatorAddress].MinipoolValidators =
		[
			..context.Nodes[nodeOperatorAddress].MinipoolValidators, entry,
		];
	}

	private async Task EventAddNewMegapoolValidator(
		NodesSyncContext context, MegapoolValidatorEnqueuedEventDTO @event, FilterLog log,
		CancellationToken cancellationToken = default)
	{
		string megapoolAddress = log.Address.ConvertToEthereumChecksumAddress();
		RocketMegapoolDelegateService megapoolDelegate = new(context.Web3, megapoolAddress);

		context.ValidatorInfo.MegapoolNodeOperatorMap.TryGetValue(megapoolAddress, out string? nodeOperatorAddress);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			nodeOperatorAddress = await FetchNodeOperatorAddressFromMegapoolAddress(
				context, log.BlockNumber, megapoolAddress, megapoolDelegate, cancellationToken);

			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
			{
				return;
			}

			context.ValidatorInfo.MegapoolNodeOperatorMap[megapoolAddress] = nodeOperatorAddress;

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

		ValidatorIndexEntry entry = new()
		{
			NodeAddress = nodeOperatorAddress.HexToByteArray(),
			MegapoolAddress = megapoolAddress.HexToByteArray(),
			MegapoolIndex = (int)@event.ValidatorId,
			PubKey = validatorInfo.ReturnValue1.PubKey,
		};

		Validator validator = new()
		{
			NodeAddress = entry.NodeAddress,
			MegapoolAddress = entry.MegapoolAddress,
			MegapoolIndex = entry.MegapoolIndex,
			PubKey = entry.PubKey,
			ExpressTicketUsed = validatorInfo.ReturnValue1.ExpressUsed,
			Status = ValidatorStatus.Created,
			Bond = 4, // TODO: Saturn2
			Type = ValidatorType.Megapool,
			History =
			[
				new ValidatorHistory
				{
					Status = ValidatorStatus.Created,
					Timestamp = (long)@event.Time,
				},
			],
		};

		context.ValidatorInfo.MegapoolValidators.TryAdd(megapoolAddress, []);
		context.ValidatorInfo.MegapoolValidators[megapoolAddress][validator.MegapoolIndex.Value] = validator;

		// TODO: Use list
		context.Nodes[nodeOperatorAddress].MegapoolValidators =
		[
			..context.Nodes[nodeOperatorAddress].MegapoolValidators, entry,
		];

		if (!validatorInfo.ReturnValue1.ExpressUsed)
		{
			context.QueueInfo.StandardQueue.Add(entry);
		}
		else
		{
			context.QueueInfo.ExpressQueue.Add(entry);
		}

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)@event.Time).DateTime);

		context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() + 1;
		context.QueueInfo.DailyEnqueued[key] = context.QueueInfo.DailyEnqueued.GetValueOrDefault(key) + 1;
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
			nodeDetails =
				await Policy.ExecuteAsync(() => latestRocketNodeManager.GetNodeDetailsQueryAsync(@event.Node));
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

	private async Task EventUpdateMegapoolValidatorAsync(
		NodesSyncContext context, string megapoolAddress, int validatorId, ValidatorStatus status,
		BigInteger eventTime, FilterLog log, CancellationToken cancellationToken)
	{
		RocketMegapoolDelegateService megapoolDelegate = new(context.Web3, megapoolAddress);

		context.ValidatorInfo.MegapoolNodeOperatorMap.TryGetValue(megapoolAddress, out string? nodeOperatorAddress);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			nodeOperatorAddress = await FetchNodeOperatorAddressFromMegapoolAddress(
				context, log.BlockNumber, megapoolAddress, megapoolDelegate, cancellationToken);

			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
			{
				return;
			}

			context.ValidatorInfo.MegapoolNodeOperatorMap[megapoolAddress] = nodeOperatorAddress;
		}

		context.ValidatorInfo.MegapoolValidators.TryAdd(megapoolAddress, []);

		if (!context.ValidatorInfo.MegapoolValidators[megapoolAddress].ContainsKey(validatorId))
		{
			context.ValidatorInfo.MegapoolValidators[megapoolAddress][validatorId] =
				(await Storage.ReadAsync<Validator>(
					Keys.MegapoolValidator(megapoolAddress, validatorId), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.ValidatorInfo.MegapoolValidators[megapoolAddress][validatorId] =
			context.ValidatorInfo.MegapoolValidators[megapoolAddress][validatorId] with
			{
				Status = status,
				History =
				[
					.. context.ValidatorInfo.MegapoolValidators[megapoolAddress][validatorId].History,
					new ValidatorHistory
					{
						Status = status,
						Timestamp = (long)eventTime,
					},
				],
			};

		// TODO: Why Staking?
		if (status == ValidatorStatus.Staking || status == ValidatorStatus.Dequeued)
		{
			int h = 0;

			// TODO: Sequence Equal?
			h += context.QueueInfo.StandardQueue.RemoveAll(x =>
				x.PubKey == context.ValidatorInfo.MegapoolValidators[megapoolAddress][validatorId].PubKey);
			h += context.QueueInfo.ExpressQueue.RemoveAll(x =>
				x.PubKey == context.ValidatorInfo.MegapoolValidators[megapoolAddress][validatorId].PubKey);

			Debug.Assert(h == 1, "Only one element should be removed");

			SortedList<DateOnly, int> dictionary =
				status == ValidatorStatus.Staking
					? context.QueueInfo.DailyDequeued
					: context.QueueInfo.DailyVoluntaryExits;
			DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventTime).DateTime);
			dictionary[key] = dictionary.GetValueOrDefault(key) + 1;

			context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() - 1;
		}
	}

	private async Task EventUpdateMinipoolValidatorAsync(
		NodesSyncContext context, string minipoolAddress, ValidatorStatus status, byte[]? pubKey,
		BigInteger eventTime, FilterLog log,
		CancellationToken cancellationToken = default)
	{
		if (!context.ValidatorInfo.MinipoolValidatorIndex.ContainsKey(minipoolAddress))
		{
			return;
		}

		RocketMinipoolDelegateService minipoolDelegate = new(context.Web3, minipoolAddress);
		context.ValidatorInfo.MinipoolNodeOperatorMap.TryGetValue(minipoolAddress, out string? nodeOperatorAddress);

		if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
		{
			nodeOperatorAddress = await FetchNodeOperatorAddressFromMinipoolAddress(
				context, log.BlockNumber, minipoolAddress, minipoolDelegate, cancellationToken);

			if (string.IsNullOrWhiteSpace(nodeOperatorAddress))
			{
				return;
			}

			context.ValidatorInfo.MinipoolNodeOperatorMap[minipoolAddress] = nodeOperatorAddress;
		}

		if (!context.ValidatorInfo.MinipoolValidators.ContainsKey(minipoolAddress))
		{
			context.ValidatorInfo.MinipoolValidators[minipoolAddress] = (await Storage.ReadAsync<Validator>(
					Keys.MinipoolValidator(minipoolAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		context.ValidatorInfo.MinipoolValidators[minipoolAddress] =
			context.ValidatorInfo.MinipoolValidators[minipoolAddress] with
			{
				Status = status,
				History =
				[
					.. context.ValidatorInfo.MinipoolValidators[minipoolAddress].History,
					new ValidatorHistory
					{
						Status = status,
						Timestamp = (long)eventTime,
					},
				],
			};

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventTime).DateTime);

		switch (status)
		{
			case ValidatorStatus.InQueue:
				context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() + 1;
				context.QueueInfo.DailyEnqueued[key] = context.QueueInfo.DailyEnqueued.GetValueOrDefault(key) + 1;
				break;

			case ValidatorStatus.Dequeued:
				context.QueueInfo.DailyDequeued[key] = context.QueueInfo.DailyDequeued.GetValueOrDefault(key) + 1;
				context.QueueInfo.TotalQueueCount[key] = context.QueueInfo.TotalQueueCount.GetLatestOrDefault() - 1;
				break;

			case ValidatorStatus.PreStaked:
				context.ValidatorInfo.MinipoolValidatorIndex[minipoolAddress] =
					context.ValidatorInfo.MinipoolValidatorIndex[minipoolAddress] with
					{
						PubKey = pubKey,
					};

				context.ValidatorInfo.MinipoolValidators[minipoolAddress] =
					context.ValidatorInfo.MinipoolValidators[minipoolAddress] with
					{
						PubKey = pubKey,
					};

				context.Nodes[nodeOperatorAddress].MinipoolValidators.ReplaceWhere(
					x => x.MinipoolAddress!.SequenceEqual(minipoolAddress.HexToByteArray()),
					x => x with
					{
						PubKey = pubKey,
					});

				break;
		}
	}

	private async Task<string?> FetchNodeOperatorAddressFromMegapoolAddress(
		NodesSyncContext context, HexBigInteger blockNumber, string megapoolAddress,
		RocketMegapoolDelegateService megapoolDelegate, CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress =
			await megapoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(blockNumber));

		// If not found might be megapool from different rocket pool version
		if (!context.NodeIndex.ContainsKey(nodeOperatorAddress))
		{
			logger.LogWarning(
				"Node operator {NodeOperatorAddress} for {MegapoolAddress} not found in index.", nodeOperatorAddress,
				megapoolAddress);
			return null;
		}

		try
		{
			// Can happen if the same node operator address is used for multiple rocket pool deployments
			if (!string.Equals(
					await context.RocketNodeManager.GetMegapoolAddressQueryAsync(nodeOperatorAddress), megapoolAddress,
					StringComparison.OrdinalIgnoreCase))
			{
				logger.LogWarning(
					"Node operator {NodeOperatorAddress} found in index but megapool address {MegapoolAddress} does not match.",
					nodeOperatorAddress, megapoolAddress);
				return null;
			}
		}
		catch (Exception e)
		{
			// Not implemented, cannot rely on version query
			return null;
		}

		if (!context.Nodes.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes[nodeOperatorAddress] =
				(await Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		return nodeOperatorAddress;
	}

	private async Task<string?> FetchNodeOperatorAddressFromMinipoolAddress(
		NodesSyncContext context, HexBigInteger blockNumber, string minipoolAddress,
		RocketMinipoolDelegateService minipoolDelegate, CancellationToken cancellationToken = default)
	{
		string nodeOperatorAddress =
			await minipoolDelegate.GetNodeAddressQueryAsync(new BlockParameter(blockNumber));

		// If not found might be minipool from different rocket pool version
		if (!context.NodeIndex.ContainsKey(nodeOperatorAddress))
		{
			logger.LogDebug(
				"Node operator {NodeOperatorAddress} for {MinipoolAddress} not found in index.", nodeOperatorAddress,
				minipoolAddress);
			return null;
		}

		// Can happen if the same node operator address is used for multiple rocket pool deployments
		if (!await context.RocketMinipoolManager.GetMinipoolExistsQueryAsync(nodeOperatorAddress))
		{
			logger.LogDebug(
				"Node operator {NodeOperatorAddress} found in index but minipool address {MinipoolAddress} does not exist.",
				nodeOperatorAddress, minipoolAddress);
			return null;
		}

		if (!context.Nodes.ContainsKey(nodeOperatorAddress))
		{
			context.Nodes[nodeOperatorAddress] =
				(await Storage.ReadAsync<Node>(Keys.Node(nodeOperatorAddress), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		return nodeOperatorAddress;
	}
}

public static class CollectionExtensions
{
	public static void ReplaceWhere<T>(this T[] elements, Predicate<T> predicate, Func<T, T> replaceFunc)
	{
		int index = Array.FindIndex(elements, predicate);

		if (index == -1)
		{
			throw new InvalidOperationException("Element not found");
		}

		elements[index] = replaceFunc(elements[index]);
	}
}

public static class ValidatorExtensions
{
	public static ValidatorStatus ToValidatorStatus(this byte status) =>
		status switch
		{
			0 => ValidatorStatus.Created,
			1 => ValidatorStatus.PreLaunch,
			2 => ValidatorStatus.Staking,
			3 => ValidatorStatus.Exited,
			4 => ValidatorStatus.Dissolved,
			_ => throw new ArgumentException("Unknown status", nameof(status)),
		};
}