using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketDAONodeTrustedUpgrade.ContractDefinition;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class ContractsSync
{
	private readonly IConfiguration configuration;

	private readonly ReadOnlyDictionary<byte[], string> contractAddresses;
	private readonly ILogger<ContractsSync> logger;

	// TODO: Inject
	private readonly AsyncRetryPolicy policy;
	private readonly ReadOnlyDictionary<byte[], string> upgradeContractAddresses;

	private readonly Storage storage;

	public ContractsSync(IConfiguration configuration, Storage storage, ILogger<ContractsSync> logger)
	{
		this.configuration = configuration;
		this.storage = storage;
		this.logger = logger;

		this.policy = Policy
			.Handle<RpcClientUnknownException>(
				x => (x.InnerException as HttpRequestException)?.StatusCode == HttpStatusCode.TooManyRequests)
			.WaitAndRetryAsync(
				Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(10), 5),
				(exception, timeSpan, retryCount, _) =>
				{
					logger.LogDebug(
						$"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to {exception.Message}");
				});

		// Lookups for addresses
		this.contractAddresses = new ReadOnlyDictionary<byte[], string>(
			new Dictionary<byte[], string>(
				Contracts.Names.Select(x => new KeyValuePair<byte[], string>(x.Sha3(), x)), new ByteArrayComparer()));
		this.upgradeContractAddresses = new ReadOnlyDictionary<byte[], string>(
			new Dictionary<byte[], string>(
				Contracts.UpgradeContractNames.Select(x => new KeyValuePair<byte[], string>(x.Sha3(), x)),
				new ByteArrayComparer()));
	}

	public async Task UpdateAndPublishAsync(CancellationToken cancellationToken = default)
	{
		ContractsSnapshot snapshot = await storage.ReadAsync<ContractsSnapshot>("contracts-snapshot.msgpack", cancellationToken);
		snapshot = await UpdateSnapshotAsync(snapshot, cancellationToken);
		await storage.WriteAsync("contracts-snapshot.msgpack", snapshot, cancellationToken);
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

	private async Task<ulong> ProcessBootstrapContractsAsync(
		Dictionary<string, RocketpoolContract> contracts,
		Dictionary<string, RocketpoolUpgradeContract> upgradeContracts, RocketStorageService rocketStorage,
		string rocketStorageContractAddress, ulong latestBlock)
	{
		ulong rocketpoolDeployedBlock =
			(ulong)await this.policy.ExecuteAsync(() => rocketStorage.GetUintQueryAsync("deploy.block".Sha3()));
		this.logger.LogInformation("Rocketpool Deployment Block: {Block}", rocketpoolDeployedBlock);

		VersionedRocketpoolUpgradeContract upgradeContract = new()
		{
			ActivationHeight = rocketpoolDeployedBlock,
			ActivationMethod = "bootstrap",
			Address = rocketStorageContractAddress,
			IsExecuted = false,
		};

		upgradeContracts["rocketStorage"] =
			new RocketpoolUpgradeContract
			{
				Name = "rocketStorage",
				Versions =
				[
					upgradeContract,
				],
			};

		await ProcessUpgradeContractAsync(
			contracts, upgradeContract, rocketStorage, rocketStorage.GetDeployedStatusQueryAsync,
			"bootstrap", rocketpoolDeployedBlock, latestBlock);

		return rocketpoolDeployedBlock;
	}

	private async Task ProcessContractAddedEventAsync(
		Dictionary<string, RocketpoolContract> contracts,
		Dictionary<string, RocketpoolUpgradeContract> upgradeContracts, Web3 web3, RocketStorageService rocketStorage,
		byte[] contractNameHash, string contractAddress, ulong currentBlock, ulong latestBlock)
	{
		if (this.upgradeContractAddresses.TryGetValue(contractNameHash, out string? upgradeContractName))
		{
			this.logger.LogInformation("New upgrade contract found {ContractName}", upgradeContractName);

			if (!upgradeContracts.ContainsKey(upgradeContractName))
			{
				upgradeContracts[upgradeContractName] =
					new RocketpoolUpgradeContract { Name = upgradeContractName, Versions = [], };
			}

			VersionedRocketpoolUpgradeContract upgradeContract = new()
			{
				ActivationHeight = (uint)currentBlock,
				ActivationMethod = "rocketDAONodeTrustedUpgrade",
				Address = contractAddress,
				IsExecuted = false,
			};

			upgradeContracts[upgradeContractName] =
				upgradeContracts[upgradeContractName] with
				{
					Versions =
					[
						..upgradeContracts[upgradeContractName].Versions,
						upgradeContract,
					],
				};

			await ProcessUpgradeContractAsync(
				contracts, upgradeContract, rocketStorage,
				blockParameter => GetExecutedFunction(web3, contractAddress).CallAsync<bool>(blockParameter),
				"rocketDAONodeTrustedUpgrade", currentBlock, latestBlock);
		}
		else if (this.contractAddresses.TryGetValue(contractNameHash, out string? contractName))
		{
			UpdateContractAddressForBlock(
				contracts, contractAddress, contractName, "rocketDAONodeTrustedUpgrade", currentBlock);
		}
		else
		{
			// TODO: Might be an unknown update contract, check for execute first
			this.logger.LogWarning("Unknown contract with address {Address}", contractAddress);
			UpdateContractAddressForBlock(
				contracts, contractAddress, $"unknown ({contractAddress[..6]})",
				"rocketDAONodeTrustedUpgrade", currentBlock);
		}
	}

	private async Task ProcessUpgradeContractAsync(
		Dictionary<string, RocketpoolContract> contracts, VersionedRocketpoolUpgradeContract upgradeContract,
		RocketStorageService rocketStorage,
		Func<BlockParameter, Task<bool>> executionFunc, string activationMethod, ulong activationHeight,
		ulong latestBlock)
	{
		ulong? executionHeight = await Helper.FindFirstBlock(
			blockParameter => this.policy.ExecuteAsync(() => executionFunc(blockParameter)),
			activationHeight, latestBlock,
			TimeSpan.FromDays(1).BlockCount());

		if (executionHeight == null)
		{
			this.logger.LogInformation("Execution block not found");
			return;
		}

		this.logger.LogInformation("Executed in block {Block}", executionHeight);

		upgradeContract.ExecutionHeight = executionHeight;
		upgradeContract.IsExecuted = true;

		foreach (string contractName in Contracts.Names)
		{
			await TryUpdateContractAddressForBlockAsync(
				contracts, rocketStorage, contractName, activationMethod, executionHeight.Value);
		}
	}



	private async Task TryUpdateContractAddressForBlockAsync(
		Dictionary<string, RocketpoolContract> contracts,
		RocketStorageService rocketStorage, string contractName, string activationMethod, ulong currentBlock)
	{
		string address = await this.policy.ExecuteAsync(
			() => rocketStorage.GetAddressQueryAsync(contractName, new BlockParameter(currentBlock)));

		if (address is null or "0x0000000000000000000000000000000000000000")
		{
			return;
		}

		if (contracts.TryGetValue(contractName, out RocketpoolContract? contract) &&
			contract.Versions.LastOrDefault()?.Address == address)
		{
			return;
		}

		UpdateContractAddressForBlock(contracts, address, contractName, activationMethod, currentBlock);
	}

	private void UpdateContractAddressForBlock(
		Dictionary<string, RocketpoolContract> contracts, string address, string contractName, string activationMethod,
		ulong currentBlock)
	{
		if (!contracts.ContainsKey(contractName))
		{
			contracts[contractName] = new RocketpoolContract { Name = contractName, Versions = [], };
		}

		this.logger.LogInformation("New Address for {ContractName} found: {Address}", contractName, address);

		contracts[contractName] = contracts[contractName] with
		{
			Versions =
			[
				..contracts[contractName].Versions,
				new VersionedRocketpoolContract
				{
					ActivationHeight = currentBlock, ActivationMethod = activationMethod, Address = address,
				},
			],
		};
	}

	private async Task<ContractsSnapshot> UpdateSnapshotAsync(ContractsSnapshot snapshot,
		CancellationToken cancellationToken = default)
	{
		// TODO: Use CancellationToken
		string network = configuration.GetValue<string>("Network") ??
			throw new InvalidOperationException("Network is null");

		string rpcUrl = this.configuration.GetValue<string>($"{network}:RPCUrl") ??
			throw new InvalidOperationException($"{network}RPCUrl is null");

		string rocketStorageContractAddress =
			this.configuration.GetValue<string>($"{network}:RocketStorageContractAddress") ??
			throw new InvalidOperationException($"{network}:RocketStorageContractAddress is null");

		this.logger.LogInformation("Connecting to {Network} / {RPCUrl}", network, rpcUrl);
		Web3 web3 = new(rpcUrl);

		ulong latestBlock =
			(ulong)(await this.policy.ExecuteAsync(() => web3.Eth.Blocks.GetBlockNumber.SendRequestAsync())).Value;

		this.logger.LogInformation("Processing block from {FromBlock} to {ToBlock}", snapshot.BlockHeight, latestBlock);

		if (snapshot.BlockHeight == latestBlock)
		{
			return snapshot;
		}

		RocketStorageService rocketStorage = new(web3, rocketStorageContractAddress);

		Dictionary<string, RocketpoolContract> contracts = snapshot.Contracts.ToDictionary(x => x.Name, x => x);
		Dictionary<string, RocketpoolUpgradeContract> upgradeContracts =
			snapshot.UpgradeContracts.ToDictionary(x => x.Name, x => x);

		ulong currentBlock = snapshot.BlockHeight;

		// Process bootstrap contracts
		if (snapshot.BlockHeight == 0)
		{
			currentBlock = await ProcessBootstrapContractsAsync(
				contracts, upgradeContracts, rocketStorage, rocketStorageContractAddress, latestBlock);
		}

		foreach (var pair in upgradeContracts.SelectMany(
						x => x.Value.Versions, (parent, contract) => new { Name = parent.Key, Contract = contract, })
					.Where(x => !x.Contract.IsExecuted))
		{
			this.logger.LogInformation("Continue processing upgrade contract {ContractName}", pair.Name);

			await ProcessUpgradeContractAsync(
				contracts, pair.Contract, rocketStorage,
				blockParameter => GetExecutedFunction(web3, pair.Contract.Address)
					.CallAsync<bool>(blockParameter),
				"rocketDAONodeTrustedUpgrade", currentBlock, latestBlock);
		}

		// Assert address of rocketDAONodeTrustedUpgrade does not change (would require an updated filter expression)
		string trustedUpgradeContractAddress = await this.policy.ExecuteAsync(
			() => rocketStorage.GetAddressQueryAsync("rocketDAONodeTrustedUpgrade"));

		// Process address upgrades via upgrade contracts or DAO trusted upgrades
		do
		{
			ulong toBlock = Math.Min(currentBlock + 100000, latestBlock);

			NewFilterInput filter = new()
			{
				FromBlock = new BlockParameter(currentBlock),
				ToBlock = new BlockParameter(toBlock),
				Address = [trustedUpgradeContractAddress,],
			};

			this.logger.LogDebug(
				"Processing block {FromBlock} to {ToBlock}", filter.FromBlock.BlockNumber,
				filter.ToBlock.BlockNumber);

			FilterLog[]? logs = await this.policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));

			IEnumerable<IEventLog> contractAddedEvents =
				web3.Eth.GetEvent<ContractAddedEventDTO>().DecodeAllEventsForEvent(logs);
			IEnumerable<IEventLog> contractUpdatedEvents =
				web3.Eth.GetEvent<ContractUpgradedEventDTO>().DecodeAllEventsForEvent(logs);

			List<IEventLog> eventLogs = contractAddedEvents.Concat(contractUpdatedEvents)
				.OrderBy(x => (ulong)x.Log.BlockNumber.Value).ToList();

			foreach (IEventLog eventLog in eventLogs)
			{
				if (eventLog is EventLog<ContractAddedEventDTO> contractAdded)
				{
					await ProcessContractAddedEventAsync(
						contracts, upgradeContracts, web3, rocketStorage, contractAdded.Event.Name,
						contractAdded.Event.NewAddress, (ulong)contractAdded.Log.BlockNumber.Value, latestBlock);
				}
				else if (eventLog is EventLog<ContractUpgradedEventDTO> contractUpgraded)
				{
					await ProcessContractAddedEventAsync(
						contracts, upgradeContracts, web3, rocketStorage, contractUpgraded.Event.Name,
						contractUpgraded.Event.NewAddress, (ulong)contractUpgraded.Log.BlockNumber.Value, latestBlock);
				}
			}

			currentBlock = toBlock + 1;
		}
		while (currentBlock <= latestBlock);

		return new ContractsSnapshot
		{
			BlockHeight = latestBlock,
			Contracts = contracts.Values.OrderBy(
				x => Array.IndexOf(Contracts.Names, x.Name) == -1
					? int.MaxValue
					: Array.IndexOf(Contracts.Names, x.Name)).ToArray(),
			UpgradeContracts = upgradeContracts.Values.ToArray(),
		};
	}


}