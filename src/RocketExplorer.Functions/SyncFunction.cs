using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Core;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Functions;

public class SyncFunction
{
	private readonly ContractsSync contracts;
	private readonly TokensSync tokens;
	private readonly ILogger logger;
	private readonly NodesSync nodes;
	private readonly SyncOptions options;
	private readonly Storage storage;

	public SyncFunction(
		IOptions<SyncOptions> options, Storage storage, ContractsSync contracts, TokensSync tokens, NodesSync nodes,
		ILogger<SyncFunction> logger)
	{
		this.options = options.Value;
		this.storage = storage;
		this.contracts = contracts;
		this.tokens = tokens;
		this.nodes = nodes;
		this.logger = logger;
	}

	[Function("SyncFunction")]
	public async Task Run([TimerTrigger("*/15 * * * * *")] TimerInfo myTimer)
	{
		Web3 web3;

		if (!string.IsNullOrWhiteSpace(options.RpcBasicAuthUsername) &&
			!string.IsNullOrWhiteSpace(options.RpcBasicAuthPassword))
		{
			logger.LogInformation("Using BasicAuth...");

			byte[] byteArray = Encoding.ASCII.GetBytes($"{options.RpcBasicAuthUsername}:{options.RpcBasicAuthPassword}");
			AuthenticationHeaderValue authenticationHeaderValue = new("Basic", Convert.ToBase64String(byteArray));
			web3 = new Web3(options.RPCUrl, authenticationHeader: authenticationHeaderValue);
		}
		else
		{
			web3 = new Web3(options.RPCUrl);
		}

		BlockWithTransactions latestBlock =
			await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
		this.logger.LogInformation("Latest block: {Block}", latestBlock.Number);

		RocketStorageService rocketStorage = new(web3, this.options.RocketStorageContractAddress);

		////await storage.WriteCorsConfigurationAsync();

		logger.LogInformation("Loading {snapshot}", Keys.DashboardSnapshot);

		BlobObject<DashboardSnapshot> dashboardSnapshot =
			await storage.ReadAsync<DashboardSnapshot>(Keys.DashboardSnapshot) ??
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
		};

		await contracts.HandleBlocksAsync(
			web3, rocketStorage, new Dictionary<string, RocketPoolContract>().AsReadOnly(), dashboardInfo,
			(long)latestBlock.Number.Value);

		BlobObject<ContractsSnapshot> snapshot =
			await storage.ReadAsync<ContractsSnapshot>(Keys.ContractsSnapshotKey) ??
			throw new InvalidOperationException("Cannot read contracts snapshot from storage.");

		await tokens.HandleBlocksAsync(
			web3, rocketStorage, snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(),
			dashboardInfo, (long)latestBlock.Number.Value);

		await nodes.HandleBlocksAsync(
			web3, rocketStorage, snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(),
			dashboardInfo, (long)latestBlock.Number.Value);

		logger.LogInformation("Writing {snapshot}", Keys.DashboardSnapshot);
		await storage.WriteAsync(
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
					NodeOperators = dashboardInfo.NodeOperators,
					MinipoolValidatorsStaking = dashboardInfo.MinipoolValidatorsStaking,
					MegapoolValidatorsStaking = dashboardInfo.MegapoolValidatorsStaking,
					QueueLength = dashboardInfo.QueueLength,
				},
			});

		logger.LogInformation("Writing {snapshot}", Keys.SnapshotMetadata);
		await storage.WriteAsync(
			Keys.SnapshotMetadata, new BlobObject<SnapshotMetadata>
			{
				ProcessedBlockNumber = (long)latestBlock.Number.Value,
				Data = new SnapshotMetadata
				{
					BlockNumber = (long)latestBlock.Number.Value,
					Timestamp = (long)latestBlock.Timestamp.Value,
				},
			}, 10);
	}
}