using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;

IHost host = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration(configuration => configuration.AddJsonFile("appsettings.local.json", true, true))
	.ConfigureLogging(logging =>
	{
		logging.ClearProviders();

		Logger logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo
			.Console(theme: AnsiConsoleTheme.Code)
			.CreateLogger();
		logging.AddSerilog(logger);
	})
	.ConfigureServices((context, services) =>
	{
		string environment = context.Configuration.GetValue<string>("Environment") ??
			throw new InvalidOperationException("Environment is null");

		services.Configure<SyncOptions>(context.Configuration.GetSection(environment));

		services.AddTransient<BeaconChainService>(provider => new BeaconChainService(
			new HttpClient
			{
				BaseAddress = new Uri(provider.GetRequiredService<IOptions<SyncOptions>>().Value.BeaconChainUrl),
			}));

		services.AddTransient<GlobalIndexService>();

		services.AddTransient<ContractsSync>();
		services.AddTransient<TokensSync>();
		services.AddTransient<NodesSync>();

		services.AddTransient<Storage>();

		services.AddTransient(_ =>
		{
			AmazonS3Client s3Client = new(
				context.Configuration["BlobStorage:User"],
				context.Configuration["BlobStorage:Password"],
				new AmazonS3Config
				{
					ServiceURL = context.Configuration["BlobStorage:Url"],
					ForcePathStyle = true,
					AuthenticationRegion = context.Configuration["BlobStorage:Region"],
					RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
					ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
				});

			return s3Client;
		});
	})
	.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
SyncOptions options = host.Services.GetRequiredService<IOptions<SyncOptions>>().Value;

logger.LogInformation(
	"Using Rocket Pool environment {Environment} with rpc endpoint {RPCUrl}", options.Environment, options.RPCUrl);

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
latestBlock = await web3.Eth.Blocks
	.GetBlockWithTransactionsByNumber
	.SendRequestAsync(new BlockParameter((ulong)(latestBlock.Number.Value - 12)));

logger.LogInformation("Latest block: {Block}", latestBlock.Number);

RocketStorageService rocketStorage = new(web3, options.RocketStorageContractAddress);
Storage storage = host.Services.GetRequiredService<Storage>();

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
	RPLLegacyStakedTotal = dashboardSnapshot.Data.RPLLegacyStakedTotal,
	RPLMegapoolStakedTotal = dashboardSnapshot.Data.RPLMegapoolStakedTotal,
};

ContextBase contextBase = new()
{
	Storage = storage,
	RocketStorage = rocketStorage,
	BeaconChainService = host.Services.GetRequiredService<BeaconChainService>(),
	Contracts = new ReadOnlyDictionary<string, RocketPoolContract>(new Dictionary<string, RocketPoolContract>()),
	CurrentBlockHeight = 0,
	GlobalIndexService = host.Services.GetRequiredService<GlobalIndexService>(),
	DashboardInfo = dashboardInfo,
	Logger = logger,
	Policy = NethereumPolicies.Retry(logger),
	Web3 = web3,
	LatestBlockHeight = (long)latestBlock.Number.Value,
};

ContractsSync contracts = host.Services.GetRequiredService<ContractsSync>();
await contracts.HandleBlocksAsync(contextBase);

BlobObject<ContractsSnapshot> snapshot =
	await storage.ReadAsync<ContractsSnapshot>(Keys.ContractsSnapshotKey) ??
	throw new InvalidOperationException("Cannot read contracts snapshot from storage.");

contextBase = contextBase with
{
	Contracts = snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(),
};

TokensSync tokens = host.Services.GetRequiredService<TokensSync>();
await tokens.HandleBlocksAsync(contextBase);

NodesSync nodes = host.Services.GetRequiredService<NodesSync>();
await nodes.HandleBlocksAsync(contextBase);

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
			RPLLegacyStakedTotal = dashboardInfo.RPLLegacyStakedTotal,
			RPLMegapoolStakedTotal = dashboardInfo.RPLMegapoolStakedTotal,
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

logger.LogInformation("Writing global index shards");
await contextBase.GlobalIndexService.WriteAsync((long)latestBlock.Number.Value);