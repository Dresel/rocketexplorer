using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketDAONodeTrustedUpgrade.ContractDefinition;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core.Contracts;

public class ContractsSync(IOptions<SyncOptions> options, Storage storage, ILogger<ContractsSync> logger)
	: SyncBase<ContractsSyncContext>(options, storage, logger)
{
	protected override async Task BeforeHandleBlocksAsync(
		ContractsSyncContext context, long latestBlock, CancellationToken cancellationToken)
	{
		if (context.CurrentBlockHeight == 0)
		{
			await ProcessBootstrapContractsAsync(context, latestBlock);
		}

		await ContinueProcessingUpgradeContractsAsync(context, latestBlock);
	}

	protected override async Task HandleBlocksAsync(
		ContractsSyncContext context, long fromBlock, long toBlock, long latestBlock,
		CancellationToken cancellationToken = default)
	{
		IEnumerable<IEventLog> nodeAddedEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(ContractAddedEventDTO), typeof(ContractUpgradedEventDTO),],
			context.TrustedUpgradeContractAddress, Policy);

		bool updated = false;

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			updated |= await eventLog.WhenIsAsync<ContractAddedEventDTO, bool>(
				(@event, log, _) => ProcessContractAddedEventAsync(
					context, @event.Name, @event.NewAddress, (long)log.BlockNumber.Value, latestBlock),
				cancellationToken);

			updated |= await eventLog.WhenIsAsync<ContractUpgradedEventDTO, bool>(
				(@event, log, _) => ProcessContractAddedEventAsync(
					context, @event.Name, @event.NewAddress, (long)log.BlockNumber.Value, latestBlock),
				cancellationToken);
		}

		if (updated)
		{
			await HandleBlocksAsync(context, fromBlock, toBlock, latestBlock, cancellationToken);
		}
	}

	protected override async Task<ContractsSyncContext> LoadContextAsync(
		Web3 web3, RocketStorageService rocketStorage, ReadOnlyDictionary<string, RocketPoolContract> contracts, DashboardInfo dashboardInfo,
		CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Loading {snapshot}", Keys.ContractsSnapshotKey);

		BlobObject<ContractsSnapshot> snapshot =
			await Storage.ReadAsync<ContractsSnapshot>(Keys.ContractsSnapshotKey, cancellationToken) ??
			new BlobObject<ContractsSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new ContractsSnapshot
				{
					Contracts = [],
					UpgradeContracts = [],
				},
			};

		return new ContractsSyncContext
		{
			Storage = Storage,
			Policy = Policy,
			Logger = Logger,
			Web3 = web3,
			CurrentBlockHeight = snapshot.ProcessedBlockNumber,
			RocketStorage = rocketStorage,
			Contracts = contracts,
			DashboardInfo = dashboardInfo,
			TrustedUpgradeContractAddress = snapshot.Data.Contracts.SingleOrDefault(x => x.Name == "rocketDAONodeTrustedUpgrade")?.Versions.Select(x => x.Address).ToList() ?? [],
			ContextContracts = snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x),
			ContextUpgradeContracts = snapshot.Data.UpgradeContracts.ToDictionary(x => x.Name, x => x),
		};
	}

	protected override async Task SaveContextAsync(
		ContractsSyncContext context, CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Writing {snapshot}", Keys.ContractsSnapshotKey);

		await Storage.WriteAsync(
			Keys.ContractsSnapshotKey,
			new BlobObject<ContractsSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new ContractsSnapshot
				{
					Contracts = context.ContextContracts.Values.OrderBy(x =>
						Array.IndexOf(Ethereum.Contracts.Names, x.Name) == -1
							? int.MaxValue
							: Array.IndexOf(Ethereum.Contracts.Names, x.Name)).ToArray(),
					UpgradeContracts = context.ContextUpgradeContracts.Values.ToArray(),
				},
			}, cancellationToken: cancellationToken);
	}

	private static Function GetExecutedFunction(Web3 web3, string address)
	{
		string executedAbi = @"[
			{
				'inputs': [],
				'name': 'executed',
				'outputs': [{ 'internalType': 'bool', 'name': '', 'type': 'bool' }],
				'stateMutability': 'view',
				'type': 'function'
			}
		]";

		Contract? contract = web3.Eth.GetContract(executedAbi, address);
		Function? executedFunction = contract.GetFunction("executed");
		return executedFunction;
	}

	private static Function GetVersionFunction(Web3 web3, string address)
	{
		string versionAbi = @"[
			{
				'inputs': [],
				'name': 'version',
				'outputs': [{ 'internalType': 'uint8', 'name': '', 'type': 'uint8' }],
				'stateMutability': 'view',
				'type': 'function'
			}
		]";

		Contract? contract = web3.Eth.GetContract(versionAbi, address);
		Function? versionFunction = contract.GetFunction("version");
		return versionFunction;
	}

	private async Task ContinueProcessingUpgradeContractsAsync(ContractsSyncContext context, long latestBlock)
	{
		foreach (var pair in context.ContextUpgradeContracts.SelectMany(
						x => x.Value.Versions, (parent, contract) => new
						{
							Name = parent.Key,
							Contract = contract,
						})
					.Where(x => !x.Contract.IsExecuted))
		{
			Logger.LogInformation("Continue processing upgrade contract {ContractName}", pair.Name);

			await ProcessUpgradeContractAsync(
				context, pair.Contract,
				blockParameter => GetExecutedFunction(context.Web3, pair.Contract.Address)
					.CallAsync<bool>(blockParameter),
				"rocketDAONodeTrustedUpgrade", context.CurrentBlockHeight, latestBlock);
		}
	}

	private async Task ProcessBootstrapContractsAsync(ContractsSyncContext context, long latestBlock)
	{
		long rocketPoolDeployedBlock =
			(long)await Policy.ExecuteAsync(() => context.RocketStorage.GetUintQueryAsync("deploy.block".Sha3()));
		Logger.LogInformation("RocketPool Deployment Block: {Block}", rocketPoolDeployedBlock);

		VersionedRocketPoolUpgradeContract upgradeContract = new()
		{
			ActivationHeight = rocketPoolDeployedBlock,
			ActivationMethod = "bootstrap",
			Address = Options.RocketStorageContractAddress,
			IsExecuted = false,
		};

		context.ContextUpgradeContracts["rocketStorage"] =
			new RocketPoolUpgradeContract
			{
				Name = "rocketStorage",
				Versions =
				[
					upgradeContract,
				],
			};

		_ = await ProcessUpgradeContractAsync(
			context, upgradeContract, context.RocketStorage.GetDeployedStatusQueryAsync,
			"bootstrap", rocketPoolDeployedBlock, latestBlock);

		context.CurrentBlockHeight = rocketPoolDeployedBlock;
	}

	private async Task<bool> ProcessContractAddedEventAsync(
		ContractsSyncContext context,
		byte[] contractNameHash, string contractAddress, long currentBlock, long latestBlock)
	{
		if (context.UpgradeContractsMap.TryGetValue(contractNameHash, out string? upgradeContractName))
		{
			if (!context.ContextUpgradeContracts.ContainsKey(upgradeContractName))
			{
				context.ContextUpgradeContracts[upgradeContractName] =
					new RocketPoolUpgradeContract
					{
						Name = upgradeContractName,
						Versions = [],
					};
			}

			// Already added
			if (context.ContextUpgradeContracts[upgradeContractName].Versions.Any(x => x.Address == contractAddress))
			{
				return false;
			}

			Logger.LogInformation("New upgrade contract found {ContractName}", upgradeContractName);

			VersionedRocketPoolUpgradeContract upgradeContract = new()
			{
				ActivationHeight = (uint)currentBlock,
				ActivationMethod = "rocketDAONodeTrustedUpgrade",
				Address = contractAddress,
				IsExecuted = false,
			};

			context.ContextUpgradeContracts[upgradeContractName] =
				context.ContextUpgradeContracts[upgradeContractName] with
				{
					Versions =
					[
						..context.ContextUpgradeContracts[upgradeContractName].Versions,
						upgradeContract,
					],
				};

			return await ProcessUpgradeContractAsync(
				context, upgradeContract,
				blockParameter => GetExecutedFunction(context.Web3, contractAddress).CallAsync<bool>(blockParameter),
				upgradeContractName, currentBlock, latestBlock);
		}
		else if (context.ContractsMap.TryGetValue(contractNameHash, out string? contractName))
		{
			return await UpdateContractAddressForBlockAsync(
				context, contractAddress, contractName, "rocketDAONodeTrustedUpgrade", currentBlock);
		}
		else
		{
			// TODO: Might be an unknown update contract, check for execute first
			Logger.LogWarning("Unknown contract with address {Address}", contractAddress);
			return await UpdateContractAddressForBlockAsync(
				context, contractAddress, $"unknown ({contractAddress[..6]})",
				"rocketDAONodeTrustedUpgrade", currentBlock);
		}
	}

	private async Task<bool> ProcessUpgradeContractAsync(
		ContractsSyncContext context, VersionedRocketPoolUpgradeContract upgradeContract,
		Func<BlockParameter, Task<bool>> executionFunc, string activationMethod, long activationHeight,
		long latestBlock)
	{
		long? executionHeight = null;

		try
		{
			executionHeight = await Helper.FindFirstBlock(
				blockParameter => Policy.ExecuteAsync(() => executionFunc(blockParameter)),
				activationHeight,
				latestBlock,
				TimeSpan.FromDays(1).BlockCount());
		}
		catch
		{
			Logger.LogWarning("Executed property not found for upgrade contract {ContractName}", activationMethod);
		}

		if (executionHeight == null)
		{
			Logger.LogInformation("Execution block not found");
			return false;
		}

		Logger.LogInformation("Executed in block {Block}", executionHeight);

		upgradeContract.ExecutionHeight = (long)executionHeight;
		upgradeContract.IsExecuted = true;

		bool rocketNodeDaoTrustedUpgradeUpdated = false;

		foreach (string contractName in Ethereum.Contracts.Names)
		{
			rocketNodeDaoTrustedUpgradeUpdated |= await TryUpdateContractAddressForBlockAsync(context, contractName, activationMethod, (long)executionHeight);
		}

		return rocketNodeDaoTrustedUpgradeUpdated;
	}

	private async Task<bool> TryUpdateContractAddressForBlockAsync(
		ContractsSyncContext context, string contractName, string activationMethod, long currentBlock)
	{
		string address = await Policy.ExecuteAsync(() =>
			context.RocketStorage.GetAddressQueryAsync(contractName, new BlockParameter((ulong)currentBlock)));

		if (address is null or "0x0000000000000000000000000000000000000000")
		{
			return false;
		}

		if (context.ContextContracts.TryGetValue(contractName, out RocketPoolContract? contract) &&
			contract.Versions.LastOrDefault()?.Address == address)
		{
			return false;
		}

		return await UpdateContractAddressForBlockAsync(context, address, contractName, activationMethod, currentBlock);
	}

	private async Task<bool> UpdateContractAddressForBlockAsync(
		ContractsSyncContext context, string address, string contractName, string activationMethod,
		long currentBlock)
	{
		if (!context.ContextContracts.ContainsKey(contractName))
		{
			context.ContextContracts[contractName] = new RocketPoolContract
			{
				Name = contractName,
				Versions = [],
			};
		}

		// Already added
		if (context.ContextContracts[contractName].Versions.Any(x => x.Address == address))
		{
			return false;
		}

		Logger.LogInformation("New Address for {ContractName} found: {Address}", contractName, address);

		byte? version = null;

		try
		{
			version = await GetVersionFunction(context.Web3, address)
				.CallAsync<byte>(BlockParameter.CreateLatest());
		}
		catch
		{
			logger.LogInformation("No version for {ContractName} found", contractName);
		}

		context.ContextContracts[contractName] = context.ContextContracts[contractName] with
		{
			Versions =
			[
				..context.ContextContracts[contractName].Versions,
				new VersionedRocketPoolContract
				{
					ActivationHeight = currentBlock,
					ActivationMethod = activationMethod,
					Address = address,
					Version = version,
				},
			],
		};

		if (contractName == "rocketDAONodeTrustedUpgrade")
		{
			context.TrustedUpgradeContractAddress.Add(address);
		}

		return contractName == "rocketDAONodeTrustedUpgrade";
	}
}