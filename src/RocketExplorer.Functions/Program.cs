using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocketExplorer.Core;
using RocketExplorer.Core.BeaconChain;
using RocketExplorer.Core.Contracts;
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
	});

builder.Build().Run();