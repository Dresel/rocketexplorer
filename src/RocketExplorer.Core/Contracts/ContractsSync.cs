using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketDAONodeTrustedUpgrade.ContractDefinition;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core.Contracts;

public class ContractsSync(IOptions<SyncOptions> syncOptions, GlobalContext globalContext)
	: SyncBase(syncOptions, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);
		GlobalContext.ContractsContext.ProcessingCompletionSource.TrySetResult();
	}

	protected override async Task OnHandleBlocksErrorAsync(Exception e, CancellationToken cancellationToken)
	{
		await base.OnHandleBlocksErrorAsync(e, cancellationToken);
		GlobalContext.ContractsContext.ProcessingCompletionSource.TrySetException(e);
	}

	protected override async Task BeforeHandleBlocksAsync(CancellationToken cancellationToken)
	{
		if (await GetCurrentBlockHeightAsync(cancellationToken) == 0)
		{
			await ProcessBootstrapContractsAsync(GlobalContext, cancellationToken);
		}

		await ContinueProcessingUpgradeContractsAsync(GlobalContext);
	}

	protected override Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(GlobalContext.ContractsContext.CurrentBlockHeight);

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		IEnumerable<IEventLog> nodeAddedEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(ContractAddedEventDTO), typeof(ContractUpgradedEventDTO),],
			GlobalContext.ContractsContext.TrustedUpgradeContractAddress, GlobalContext.Policy);

		bool updated = false;

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			updated |= await eventLog.WhenIsAsync<ContractAddedEventDTO, bool>(
				(@event, log, _) => ProcessContractAddedEventAsync(
					GlobalContext,
					@event.Name, @event.NewAddress, (long)log.BlockNumber.Value, cancellationToken),
				cancellationToken);

			updated |= await eventLog.WhenIsAsync<ContractUpgradedEventDTO, bool>(
				(@event, log, _) => ProcessContractAddedEventAsync(
					GlobalContext,
					@event.Name, @event.NewAddress, (long)log.BlockNumber.Value, cancellationToken),
				cancellationToken);
		}

		if (updated)
		{
			await HandleBlocksAsync(fromBlock, toBlock, cancellationToken);
		}
	}

	protected override Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		GlobalContext.ContractsContext.CurrentBlockHeight = currentBlockHeight;
		return Task.CompletedTask;
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

	private async Task ContinueProcessingUpgradeContractsAsync(GlobalContext globalContext)
	{
		foreach (var pair in globalContext.ContractsContext.ContextUpgradeContracts.SelectMany(
						x => x.Value.Versions, (parent, contract) => new
						{
							Name = parent.Key,
							Contract = contract,
						})
					.Where(x => !x.Contract.IsExecuted))
		{
			globalContext.GetLogger<ContractsSync>().LogInformation(
				"Continue processing upgrade contract {ContractName}", pair.Name);

			await ProcessUpgradeContractAsync(
				globalContext,
				pair.Contract,
				blockParameter => GetExecutedFunction(globalContext.Services.Web3, pair.Contract.Address)
					.CallAsync<bool>(blockParameter),
				pair.Name, globalContext.ContractsContext.CurrentBlockHeight);
		}
	}

	private async Task ProcessBootstrapContractsAsync(GlobalContext globalContext, CancellationToken cancellationToken = default)
	{
		long rocketPoolDeployedBlock =
			(long)await globalContext.Policy.ExecuteAsync(() =>
				globalContext.Services.RocketStorage.GetUintQueryAsync("deploy.block".Sha3()));
		globalContext.GetLogger<ContractsSync>().LogInformation(
			"RocketPool Deployment Block: {Block}", rocketPoolDeployedBlock);

		VersionedRocketPoolUpgradeContract upgradeContract = new()
		{
			ActivationHeight = rocketPoolDeployedBlock,
			ActivationMethod = "bootstrap",
			Address = Options.RocketStorageContractAddress,
			IsExecuted = false,
		};

		globalContext.ContractsContext.ContextUpgradeContracts["rocketStorage"] =
			new RocketPoolUpgradeContract
			{
				Name = "rocketStorage",
				Versions =
				[
					upgradeContract,
				],
			};

		_ = await ProcessUpgradeContractAsync(
			globalContext,
			upgradeContract, globalContext.Services.RocketStorage.GetDeployedStatusQueryAsync,
			"bootstrap", rocketPoolDeployedBlock);

		await SetCurrentBlockHeightAsync(rocketPoolDeployedBlock, cancellationToken);
		globalContext.ContractsContext.CurrentBlockHeight = rocketPoolDeployedBlock;
	}

	private async Task<bool> ProcessContractAddedEventAsync(
		GlobalContext globalContext,
		byte[] contractNameHash, string contractAddress, long currentBlock,
		CancellationToken cancellationToken = default)
	{
		if (globalContext.ContractsContext.UpgradeContractsMap.TryGetValue(
				contractNameHash, out string? upgradeContractName))
		{
			if (!globalContext.ContractsContext.ContextUpgradeContracts.ContainsKey(upgradeContractName))
			{
				globalContext.ContractsContext.ContextUpgradeContracts[upgradeContractName] =
					new RocketPoolUpgradeContract
					{
						Name = upgradeContractName,
						Versions = [],
					};
			}

			// Already added
			if (globalContext.ContractsContext.ContextUpgradeContracts[upgradeContractName].Versions
				.Any(x => x.Address == contractAddress))
			{
				return false;
			}

			globalContext.GetLogger<ContractsSync>().LogInformation(
				"New upgrade contract found {ContractName}", upgradeContractName);

			VersionedRocketPoolUpgradeContract upgradeContract = new()
			{
				ActivationHeight = currentBlock,
				ActivationMethod = "rocketDAONodeTrustedUpgrade",
				Address = contractAddress,
				IsExecuted = false,
			};

			globalContext.ContractsContext.ContextUpgradeContracts[upgradeContractName] =
				globalContext.ContractsContext.ContextUpgradeContracts[upgradeContractName] with
				{
					Versions =
					[
						..globalContext.ContractsContext.ContextUpgradeContracts[upgradeContractName].Versions,
						upgradeContract,
					],
				};

			return await ProcessUpgradeContractAsync(
				globalContext,
				upgradeContract,
				blockParameter => GetExecutedFunction(globalContext.Services.Web3, contractAddress)
					.CallAsync<bool>(blockParameter),
				upgradeContractName, currentBlock);
		}

		if (globalContext.ContractsContext.ContractsMap.TryGetValue(contractNameHash, out string? contractName))
		{
			return await UpdateContractAddressForBlockAsync(
				globalContext,
				contractAddress, contractName, "rocketDAONodeTrustedUpgrade", currentBlock);
		}

		// TODO: Might be an unknown update contract, check for execute first
		globalContext.GetLogger<ContractsSync>().LogWarning("Unknown contract with address {Address}", contractAddress);
		return await UpdateContractAddressForBlockAsync(
			globalContext,
			contractAddress, $"unknown ({contractAddress[..6]})",
			"rocketDAONodeTrustedUpgrade", currentBlock);
	}

	private async Task<bool> ProcessUpgradeContractAsync(
		GlobalContext globalContext,
		VersionedRocketPoolUpgradeContract upgradeContract,
		Func<BlockParameter, Task<bool>> executionFunc, string activationMethod, long activationHeight)
	{
		long? executionHeight = null;

		try
		{
			executionHeight = await Helper.FindFirstBlockAsync(
				blockParameter => globalContext.Policy.ExecuteAsync(() => executionFunc(blockParameter)),
				activationHeight,
				globalContext.LatestBlockHeight,
				TimeSpan.FromDays(1).BlockCount());
		}
		catch
		{
			globalContext.GetLogger<ContractsSync>().LogWarning(
				"Executed property not found for upgrade contract {ContractName}", activationMethod);
		}

		executionHeight ??= await Helper.FindFirstBlockAsync(
			blockParameter => globalContext.Policy.ExecuteAsync(async () =>
			{
				string version = await globalContext.Services.RocketStorage.GetStringQueryAsync(
					"protocol.version".Sha3(), blockParameter);

				return !string.IsNullOrWhiteSpace(version) &&
					globalContext.ContractsContext.ProtocolVersion?.Equals(
						version, StringComparison.OrdinalIgnoreCase) != true;
			}),
			activationHeight,
			globalContext.LatestBlockHeight,
			TimeSpan.FromDays(1).BlockCount());

		if (executionHeight == null)
		{
			globalContext.GetLogger<ContractsSync>().LogInformation("Neither execution block nor version change found");
			return false;
		}

		globalContext.ContractsContext.ProtocolVersion = await globalContext.Policy.ExecuteAsync(() => globalContext.Services.RocketStorage.GetStringQueryAsync(
			"protocol.version".Sha3(), new BlockParameter((ulong)executionHeight)));

		globalContext.GetLogger<ContractsSync>().LogInformation("Executed in block {Block}", executionHeight);

		upgradeContract.ExecutionHeight = (long)executionHeight;
		upgradeContract.IsExecuted = true;

		bool rocketNodeDaoTrustedUpgradeUpdated = false;

		foreach (string contractName in Ethereum.Contracts.Names)
		{
			rocketNodeDaoTrustedUpgradeUpdated |= await TryUpdateContractAddressForBlockAsync(
				globalContext,
				contractName, activationMethod, (long)executionHeight);
		}

		return rocketNodeDaoTrustedUpgradeUpdated;
	}

	private async Task<bool> TryUpdateContractAddressForBlockAsync(
		GlobalContext globalContext,
		string contractName, string activationMethod, long currentBlock)
	{
		string address = await globalContext.Policy.ExecuteAsync(() =>
			globalContext.Services.RocketStorage.GetAddressQueryAsync(
				contractName, new BlockParameter((ulong)currentBlock)));

		if (address is null or "0x0000000000000000000000000000000000000000")
		{
			return false;
		}

		if (globalContext.ContractsContext.ContextContracts.TryGetValue(
				contractName, out RocketPoolContract? contract) &&
			contract.Versions.LastOrDefault()?.Address == address)
		{
			return false;
		}

		return await UpdateContractAddressForBlockAsync(
			globalContext, address, contractName, activationMethod, currentBlock);
	}

	private async Task<bool> UpdateContractAddressForBlockAsync(
		GlobalContext globalContext,
		string address, string contractName, string activationMethod,
		long currentBlock)
	{
		if (!globalContext.ContractsContext.ContextContracts.ContainsKey(contractName))
		{
			globalContext.ContractsContext.ContextContracts[contractName] = new RocketPoolContract
			{
				Name = contractName,
				Versions = [],
			};
		}

		// Already added
		if (globalContext.ContractsContext.ContextContracts[contractName].Versions.Any(x => x.Address == address))
		{
			return false;
		}

		globalContext.GetLogger<ContractsSync>().LogInformation(
			"New Address for {ContractName} found: {Address}", contractName, address);

		byte? version = null;

		try
		{
			version = await globalContext.Policy.ExecuteAsync(() => GetVersionFunction(globalContext.Services.Web3, address)
				.CallAsync<byte>(BlockParameter.CreateLatest()));
		}
		catch
		{
			globalContext.GetLogger<ContractsSync>().LogInformation(
				"No version for {ContractName} found", contractName);
		}

		globalContext.ContractsContext.ContextContracts[contractName] =
			globalContext.ContractsContext.ContextContracts[contractName] with
			{
				Versions =
				[
					..globalContext.ContractsContext.ContextContracts[contractName].Versions,
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
			globalContext.ContractsContext.TrustedUpgradeContractAddress.Add(address);
		}

		return contractName == "rocketDAONodeTrustedUpgrade";
	}
}