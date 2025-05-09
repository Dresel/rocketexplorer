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
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;

IHost host = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration(configuration => configuration.AddJsonFile("appsettings.local.json", true, true))
	.ConfigureLogging(
		logging =>
		{
			logging.ClearProviders();

			Logger logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console(theme: AnsiConsoleTheme.Code)
				.CreateLogger();
			logging.AddSerilog(logger);
		})
	.ConfigureServices(
		(context, services) =>
		{
			string environment = context.Configuration.GetValue<string>("Environment") ??
				throw new InvalidOperationException("Environment is null");

			services.Configure<SyncOptions>(context.Configuration.GetSection(environment));

			services.AddTransient<ContractsSync>();
			services.AddTransient<NodesSync>();

			services.AddTransient<Storage>();

			services.AddTransient(
				_ =>
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

Web3 web3 = new(options.RPCUrl);

BlockWithTransactions latestBlock =
	await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
logger.LogInformation("Latest block: {Block}", latestBlock.Number);

RocketStorageService rocketStorage = new(web3, options.RocketStorageContractAddress);
Storage storage = host.Services.GetRequiredService<Storage>();

////await storage.WriteCorsConfigurationAsync();

ContractsSync contracts = host.Services.GetRequiredService<ContractsSync>();
await contracts.HandleBlocksAsync(
	web3, rocketStorage, new Dictionary<string, RocketPoolContract>().AsReadOnly(), (long)latestBlock.Number.Value);

BlobObject<ContractsSnapshot> snapshot =
	await storage.ReadAsync<ContractsSnapshot>(ContractsSync.ContractsSnapshotKey) ??
	throw new InvalidOperationException("Cannot read contracts snapshot from storage.");

NodesSync nodes = host.Services.GetRequiredService<NodesSync>();
await nodes.HandleBlocksAsync(
	web3, rocketStorage, snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(),
	(long)latestBlock.Number.Value);

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