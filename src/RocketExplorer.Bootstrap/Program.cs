using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RocketExplorer.Core;
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
			services.AddTransient<ContractsSync>();
			services.AddTransient<Storage>();

			services.AddTransient<AmazonS3Client>(
				provider =>
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

ContractsSync bootstrapper = host.Services.GetRequiredService<ContractsSync>();
await bootstrapper.UpdateAndPublishAsync();