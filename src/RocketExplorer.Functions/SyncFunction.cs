using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Core;
using RocketExplorer.Core.BeaconChain;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Functions;

public class SyncFunction
{
	private readonly BeaconChainService beaconChainService;
	private readonly ContractsSync contracts;
	private readonly GlobalIndexService globalIndexService;
	private readonly ILogger logger;
	private readonly NodesSync nodes;
	private readonly SyncOptions options;
	private readonly Storage storage;
	private readonly TokensSync tokens;

	public SyncFunction(
		IOptions<SyncOptions> options, Storage storage, BeaconChainService beaconChainService,
		GlobalIndexService globalIndexService, ContractsSync contracts, TokensSync tokens, NodesSync nodes,
		ILogger<SyncFunction> logger)
	{
		this.options = options.Value;
		this.storage = storage;
		this.beaconChainService = beaconChainService;
		this.contracts = contracts;
		this.tokens = tokens;
		this.nodes = nodes;
		this.logger = logger;
		this.globalIndexService = globalIndexService;
	}

	[Function("SyncFunction")]
	public async Task Run([TimerTrigger("%SyncFunctionSchedule%")] TimerInfo myTimer)
	{
		Web3 web3;

		if (!string.IsNullOrWhiteSpace(this.options.RpcBasicAuthUsername) &&
			!string.IsNullOrWhiteSpace(this.options.RpcBasicAuthPassword))
		{
			this.logger.LogInformation("Using BasicAuth...");

			byte[] byteArray = Encoding.ASCII.GetBytes(
				$"{this.options.RpcBasicAuthUsername}:{this.options.RpcBasicAuthPassword}");
			AuthenticationHeaderValue authenticationHeaderValue = new("Basic", Convert.ToBase64String(byteArray));
			web3 = new Web3(this.options.RPCUrl, authenticationHeader: authenticationHeaderValue);
		}
		else
		{
			web3 = new Web3(this.options.RPCUrl);
		}

		BlockWithTransactions latestBlock =
			await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
		latestBlock = await web3.Eth.Blocks
			.GetBlockWithTransactionsByNumber
			.SendRequestAsync(new BlockParameter((ulong)(latestBlock.Number.Value - 12)));

		this.logger.LogInformation("Latest block: {Block}", latestBlock.Number);

		RocketStorageService rocketStorage = new(web3, this.options.RocketStorageContractAddress);

		this.logger.LogInformation("Loading {snapshot}", Keys.DashboardSnapshot);

		BlobObject<DashboardSnapshot> dashboardSnapshot =
			await this.storage.ReadAsync<DashboardSnapshot>(Keys.DashboardSnapshot) ??
			new BlobObject<DashboardSnapshot>
			{
				ProcessedBlockNumber = (long)latestBlock.Number.Value,
				Data = new DashboardSnapshot
				{
					RPLOldTotalSupply = 0,
					RPLTotalSupply = 0,
					RETHTotalSupply = 0,
					RPLSwapped = 0,
					RPLLegacyStakedTotal = 0,
					RPLMegapoolStakedTotal = 0,
					NodeOperators = 0,
					MinipoolValidatorsStaking = 0,
					MegapoolValidatorsStaking = 0,
					QueueLength = 0,
				},
			};

		DashboardInfo dashboardInfo = new()
		{
			RPLOldSupplyTotal = dashboardSnapshot.Data.RPLOldTotalSupply,
			RPLSupplyTotal = dashboardSnapshot.Data.RPLTotalSupply,
			RETHSupplyTotal = dashboardSnapshot.Data.RETHTotalSupply,
			RPLSwappedTotal = dashboardSnapshot.Data.RPLSwapped,
			NodeOperators = dashboardSnapshot.Data.NodeOperators,
			MinipoolValidatorsStaking = dashboardSnapshot.Data.MinipoolValidatorsStaking,
			MegapoolValidatorsStaking = dashboardSnapshot.Data.MegapoolValidatorsStaking,
			QueueLength = dashboardSnapshot.Data.QueueLength,
			RPLLegacyStakedTotal = dashboardSnapshot.Data.RPLLegacyStakedTotal,
			RPLMegapoolStakedTotal = dashboardSnapshot.Data.RPLMegapoolStakedTotal,
			RockRETHSupplyTotal = dashboardSnapshot.Data.RockRETHTotalSupply,
		};

		ContextBase contextBase = new()
		{
			Storage = this.storage,
			RocketStorage = rocketStorage,
			BeaconChainService = this.beaconChainService,
			Contracts =
				new ReadOnlyDictionary<string, RocketPoolContract>(new Dictionary<string, RocketPoolContract>()),
			CurrentBlockHeight = 0,
			GlobalIndexService = this.globalIndexService,
			DashboardInfo = dashboardInfo,
			Logger = this.logger,
			Policy = NethereumPolicies.Retry(this.logger),
			Web3 = web3,
			LatestBlockHeight = (long)latestBlock.Number.Value,
		};

		await this.contracts.HandleBlocksAsync(contextBase);

		BlobObject<ContractsSnapshot> snapshot =
			await this.storage.ReadAsync<ContractsSnapshot>(Keys.ContractsSnapshotKey) ??
			throw new InvalidOperationException("Cannot read contracts snapshot from storage.");

		contextBase = contextBase with
		{
			Contracts = snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(),
		};

		await this.tokens.HandleBlocksAsync(contextBase);
		await this.nodes.HandleBlocksAsync(contextBase);

		this.logger.LogInformation("Writing {snapshot}", Keys.DashboardSnapshot);
		await this.storage.WriteAsync(
			Keys.DashboardSnapshot,
			new BlobObject<DashboardSnapshot>
			{
				ProcessedBlockNumber = (long)latestBlock.Number.Value,
				Data = new DashboardSnapshot
				{
					RPLOldTotalSupply = dashboardInfo.RPLOldSupplyTotal,
					RPLTotalSupply = dashboardInfo.RPLSupplyTotal,
					RETHTotalSupply = dashboardInfo.RETHSupplyTotal,
					RPLSwapped = dashboardInfo.RPLSwappedTotal,
					RPLLegacyStakedTotal = dashboardInfo.RPLLegacyStakedTotal,
					RPLMegapoolStakedTotal = dashboardInfo.RPLMegapoolStakedTotal,
					NodeOperators = dashboardInfo.NodeOperators,
					MinipoolValidatorsStaking = dashboardInfo.MinipoolValidatorsStaking,
					MegapoolValidatorsStaking = dashboardInfo.MegapoolValidatorsStaking,
					QueueLength = dashboardInfo.QueueLength,
				},
			});

		this.logger.LogInformation("Writing {snapshot}", Keys.SnapshotMetadata);
		await this.storage.WriteAsync(
			Keys.SnapshotMetadata, new BlobObject<SnapshotMetadata>
			{
				ProcessedBlockNumber = (long)latestBlock.Number.Value,
				Data = new SnapshotMetadata
				{
					BlockNumber = (long)latestBlock.Number.Value,
					Timestamp = (long)latestBlock.Timestamp.Value,
				},
			}, 10);

		this.logger.LogInformation("Writing global index shards");
		await contextBase.GlobalIndexService.WriteAsync((long)latestBlock.Number.Value);
	}
}