using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using RocketExplorer.Core;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum.RocketStorage;
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

logger.LogInformation("Using Rocket Pool environment {Environment} with rpc endpoint {RPCUrl}", options.Environment, options.RPCUrl);

Web3 web3 = new(options.RPCUrl);
long latestBlock = (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;

logger.LogInformation("Latest block: {Block}", latestBlock);

RocketStorageService rocketStorage = new(web3, options.RocketStorageContractAddress);
Storage storage = host.Services.GetRequiredService<Storage>();

ContractsSync contracts = host.Services.GetRequiredService<ContractsSync>();
await contracts.HandleBlocksAsync(web3, rocketStorage, new Dictionary<string, RocketPoolContract>().AsReadOnly(), latestBlock, CancellationToken.None);

ContractsSnapshot snapshot = await storage.ReadAsync<ContractsSnapshot>(ContractsSync.ContractsSnapshotKey) ?? throw new InvalidOperationException("Cannot read contracts snapshot from storage.");

NodesSync nodes = host.Services.GetRequiredService<NodesSync>();
await nodes.HandleBlocksAsync(web3, rocketStorage, snapshot.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(), latestBlock, CancellationToken.None);