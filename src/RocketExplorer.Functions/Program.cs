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

IHostBuilder builder = new HostBuilder()
	.ConfigureFunctionsWorkerDefaults()
	.ConfigureAppConfiguration((context, configuration) =>
	{
		configuration
			.AddJsonFile("appsettings.json", true, true)
			.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", true, true)
			.AddJsonFile("appsettings.local.json", true, true);

		configuration.AddEnvironmentVariables();
	})
	.ConfigureLogging((context, logging) =>
	{
		logging.AddConfiguration(context.Configuration.GetSection("Logging"));
		logging.AddConsole();
	})
	.ConfigureServices((context, services) =>
	{
		string environment = context.Configuration.GetValue<string>("RocketEnvironment") ??
			throw new InvalidOperationException("RocketEnvironment is null");

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
	});

await builder.Build().RunAsync();