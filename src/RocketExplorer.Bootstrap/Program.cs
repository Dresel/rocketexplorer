using System.Net.Http.Headers;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using RocketExplorer.Core;
using RocketExplorer.Core.BeaconChain;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Ens;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Shared;
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
			////.WriteTo.File($"log-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt")
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
		services.AddTransient<GlobalEnsIndexService>();

		services.AddTransient<ContractsSync>();
		services.AddTransient<TokensSyncRPL>();
		services.AddTransient<TokensSyncRPLOld>();
		services.AddTransient<TokensSyncRETH>();
		services.AddTransient<TokensSyncRockRETH>();
		services.AddTransient<TokensSyncStakedRPL>();
		services.AddTransient<NodesSync>();
		services.AddTransient<EnsSync>();

		services.AddTransient<Web3>(serviceProvider =>
		{
			ILogger<Web3> logger = serviceProvider.GetRequiredService<ILogger<Web3>>();
			SyncOptions options = serviceProvider.GetRequiredService<IOptions<SyncOptions>>().Value;

			Web3 web3;

			if (!string.IsNullOrWhiteSpace(options.RpcBasicAuthUsername) &&
				!string.IsNullOrWhiteSpace(options.RpcBasicAuthPassword))
			{
				logger.LogInformation("Using BasicAuth...");

				byte[] byteArray =
					Encoding.ASCII.GetBytes($"{options.RpcBasicAuthUsername}:{options.RpcBasicAuthPassword}");
				AuthenticationHeaderValue authenticationHeaderValue = new("Basic", Convert.ToBase64String(byteArray));
				web3 = new Web3(options.RPCUrl, authenticationHeader: authenticationHeaderValue);
			}
			else
			{
				web3 = new Web3(options.RPCUrl);
			}

			return web3;
		});

		services.AddScoped<GlobalContextAccessor>();
		services.AddScoped<GlobalContext>(serviceProvider =>
			serviceProvider.GetRequiredService<GlobalContextAccessor>().GlobalContext ??
			throw new InvalidOperationException("GlobalContext not initialized or set"));

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

GlobalContext globalContext = await host.Services.CreateGlobalContextAsync();
host.Services.GetRequiredService<GlobalContextAccessor>().GlobalContext = globalContext;

// Initial sync
bool skipIndexEns = false;
////bool skipIndexEns = true;
////globalContext.Services.GlobalIndexService.SkipLoading = true;
////globalContext.Services.GlobalEnsIndexService.SkipLoading = true;

// Initial index build
////await IndexBuilder.BuildIndexesAsync(globalContext);
////logger.LogInformation("Sync completed");
////return;

Task contractsSyncTask = host.Services.GetRequiredService<ContractsSync>().HandleBlocksAsync();
Task nodesSyncTask = host.Services.GetRequiredService<NodesSync>().HandleBlocksAsync();
List<Task> tokenSyncTasks = host.Services.HandleTokenBlocksAsync();

await Task.WhenAll([contractsSyncTask, nodesSyncTask, ..tokenSyncTasks]);

Task writeContractsTask = globalContext.ContractsContext.SaveAsync(
	globalContext.Services.Storage, host.Services.GetRequiredService<ILogger<ContractsContext>>());
NodesMasterContext nodesMasterContext = await globalContext.NodesMasterContextFactory;
Task writeNodesTask = nodesMasterContext.SaveAsync(
	globalContext.Services.Storage, host.Services.GetRequiredService<ILogger<NodesMasterContext>>());

List<Task> writeTokenTasks = await host.Services.SaveTokenTasksAsync();

Task writeDashboardTask = globalContext.DashboardContext.SaveAsync(
	globalContext.Services.Storage, globalContext.LatestBlockHeight,
	host.Services.GetRequiredService<ILogger<DashboardInfo>>());

logger.LogInformation("Writing {snapshot}", Keys.SnapshotMetadata);
Task writeMetadataTask = globalContext.Services.Storage.WriteAsync(
	Keys.SnapshotMetadata, new BlobObject<SnapshotMetadata>
	{
		ProcessedBlockNumber = globalContext.LatestBlockHeight,
		Data = new SnapshotMetadata
		{
			BlockNumber = globalContext.LatestBlockHeight,
			Timestamp = (long)globalContext.LatestBlock.Timestamp.Value,
		},
	}, 10);

await Task.WhenAll([writeContractsTask, writeNodesTask, ..writeTokenTasks, writeDashboardTask, writeMetadataTask]);

if (!skipIndexEns)
{
	await host.Services.GetRequiredService<EnsSync>().HandleBlocksAsync();

	EnsContext ensContext = await globalContext.EnsContextFactory;
	Task writeEnsTask = ensContext.SaveAsync(
		globalContext.Services.Storage, host.Services.GetRequiredService<ILogger<EnsContext>>());

	Task writeIndexTask = globalContext.Services.GlobalIndexService.WriteAsync(globalContext.LatestBlockHeight);
	Task writeEnsIndexTask = globalContext.Services.GlobalEnsIndexService.WriteAsync(globalContext.LatestBlockHeight);

	await Task.WhenAll(writeEnsTask, writeIndexTask, writeEnsIndexTask);
}

logger.LogInformation("Sync completed");