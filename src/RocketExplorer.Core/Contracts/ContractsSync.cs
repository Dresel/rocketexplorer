using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketDAONodeTrustedUpgrade.ContractDefinition;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core.Contracts;

public class ContractsSync(IOptions<SyncOptions> options, Storage storage, ILogger<ContractsSync> logger)
	: SyncBase<ContractsSyncContext>(options, storage, logger)
{
	public const string ContractsSnapshotKey = "contracts-snapshot.msgpack";

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
			(ulong)fromBlock, (ulong)toBlock, [typeof(ContractAddedEventDTO), typeof(ContractUpgradedEventDTO),],
			[context.TrustedUpgradeContractAddress,], Policy);

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			await eventLog.WhenIsAsync<ContractAddedEventDTO>(
				(@event, log, _) => ProcessContractAddedEventAsync(
					context, @event.Name, @event.NewAddress, (long)log.BlockNumber.Value, latestBlock),
				cancellationToken);

			await eventLog.WhenIsAsync<ContractUpgradedEventDTO>(
				(@event, log, _) => ProcessContractAddedEventAsync(
					context, @event.Name, @event.NewAddress, (long)log.BlockNumber.Value, latestBlock),
				cancellationToken);
		}
	}

	protected override async Task<ContractsSyncContext> LoadContextAsync(
		Web3 web3, RocketStorageService rocketStorage, ReadOnlyDictionary<string, RocketPoolContract> contracts,
		CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Loading {snapshot}", ContractsSnapshotKey);

		BlobObject<ContractsSnapshot>? snapshot = await Storage.ReadAsync<ContractsSnapshot>(
			ContractsSnapshotKey, cancellationToken);

		if (snapshot == null)
		{
			return new ContractsSyncContext
			{
				Web3 = web3,
				CurrentBlockHeight = 0,
				RocketStorage = rocketStorage,
				Contracts = contracts,
				TrustedUpgradeContractAddress =
					await Policy.ExecuteAsync(() => rocketStorage.GetAddressQueryAsync("rocketDAONodeTrustedUpgrade")),
				ContextContracts = [],
				ContextUpgradeContracts = [],
			};
		}

		return new ContractsSyncContext
		{
			Web3 = web3,
			CurrentBlockHeight = snapshot.ProcessedBlockNumber,
			RocketStorage = rocketStorage,
			Contracts = contracts,
			TrustedUpgradeContractAddress =
				await Policy.ExecuteAsync(() => rocketStorage.GetAddressQueryAsync("rocketDAONodeTrustedUpgrade")),
			ContextContracts = snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x),
			ContextUpgradeContracts = snapshot.Data.UpgradeContracts.ToDictionary(x => x.Name, x => x),
		};
	}

	protected override async Task SaveContextAsync(
		ContractsSyncContext context, CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Writing {snapshot}", ContractsSnapshotKey);

		await Storage.WriteAsync(
			ContractsSnapshotKey,
			new BlobObject<ContractsSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new ContractsSnapshot
				{
					Contracts = context.ContextContracts.Values.OrderBy(
						x =>
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

		await ProcessUpgradeContractAsync(
			context, upgradeContract, context.RocketStorage.GetDeployedStatusQueryAsync,
			"bootstrap", rocketPoolDeployedBlock, latestBlock);

		context.CurrentBlockHeight = rocketPoolDeployedBlock;
	}

	private async Task ProcessContractAddedEventAsync(
		ContractsSyncContext context,
		byte[] contractNameHash, string contractAddress, long currentBlock, long latestBlock)
	{
		if (context.UpgradeContractsMap.TryGetValue(contractNameHash, out string? upgradeContractName))
		{
			Logger.LogInformation("New upgrade contract found {ContractName}", upgradeContractName);

			if (!context.ContextUpgradeContracts.ContainsKey(upgradeContractName))
			{
				context.ContextUpgradeContracts[upgradeContractName] =
					new RocketPoolUpgradeContract
					{
						Name = upgradeContractName,
						Versions = [],
					};
			}

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

			await ProcessUpgradeContractAsync(
				context, upgradeContract,
				blockParameter => GetExecutedFunction(context.Web3, contractAddress).CallAsync<bool>(blockParameter),
				upgradeContractName, currentBlock, latestBlock);
		}
		else if (context.ContractsMap.TryGetValue(contractNameHash, out string? contractName))
		{
			UpdateContractAddressForBlock(
				context, contractAddress, contractName, "rocketDAONodeTrustedUpgrade", currentBlock);
		}
		else
		{
			// TODO: Might be an unknown update contract, check for execute first
			Logger.LogWarning("Unknown contract with address {Address}", contractAddress);
			UpdateContractAddressForBlock(
				context, contractAddress, $"unknown ({contractAddress[..6]})",
				"rocketDAONodeTrustedUpgrade", currentBlock);
		}
	}

	private async Task ProcessUpgradeContractAsync(
		ContractsSyncContext context, VersionedRocketPoolUpgradeContract upgradeContract,
		Func<BlockParameter, Task<bool>> executionFunc, string activationMethod, long activationHeight,
		long latestBlock)
	{
		long? executionHeight = await Helper.FindFirstBlock(
			blockParameter => Policy.ExecuteAsync(() => executionFunc(blockParameter)),
			activationHeight, latestBlock,
			TimeSpan.FromDays(1).BlockCount());

		if (executionHeight == null)
		{
			Logger.LogInformation("Execution block not found");
			return;
		}

		Logger.LogInformation("Executed in block {Block}", executionHeight);

		upgradeContract.ExecutionHeight = (long)executionHeight;
		upgradeContract.IsExecuted = true;

		foreach (string contractName in Ethereum.Contracts.Names)
		{
			await TryUpdateContractAddressForBlockAsync(context, contractName, activationMethod, (long)executionHeight);
		}
	}

	private async Task TryUpdateContractAddressForBlockAsync(
		ContractsSyncContext context, string contractName, string activationMethod, long currentBlock)
	{
		string address = await Policy.ExecuteAsync(
			() =>
				context.RocketStorage.GetAddressQueryAsync(contractName, new BlockParameter((ulong)currentBlock)));

		if (address is null or "0x0000000000000000000000000000000000000000")
		{
			return;
		}

		if (context.ContextContracts.TryGetValue(contractName, out RocketPoolContract? contract) &&
			contract.Versions.LastOrDefault()?.Address == address)
		{
			return;
		}

		UpdateContractAddressForBlock(context, address, contractName, activationMethod, currentBlock);
	}

	private void UpdateContractAddressForBlock(
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

		Logger.LogInformation("New Address for {ContractName} found: {Address}", contractName, address);

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
				},
			],
		};
	}
}