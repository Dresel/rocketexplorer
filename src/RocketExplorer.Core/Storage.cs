using System.Diagnostics;
using System.Globalization;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Retry;
using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class Storage(IOptions<SyncOptions> options, AmazonS3Client s3Client, ILogger<Storage> logger)
{
	private readonly string bucketName = options.Value.BucketName;

	private readonly ILogger<Storage> logger = logger;

	private readonly IFormatterResolver messagePackResolver =
		CompositeResolver.Create(BigIntegerResolver.Instance, StandardResolver.Instance);

	private readonly SyncOptions options = options.Value;
	private readonly AsyncRetryPolicy retryPolicy = StoragePolicies.Retry(logger);

	private readonly AmazonS3Client s3Client = s3Client;

	public async Task WriteCorsConfigurationAsync(CancellationToken cancellationToken = default)
	{
		await this.s3Client.PutCORSConfigurationAsync(
			new PutCORSConfigurationRequest
			{
				BucketName = this.bucketName,
				Configuration = new CORSConfiguration
				{
					Rules =
					[
						new CORSRule
						{
							AllowedHeaders = ["*",],
							AllowedMethods = ["HEAD", "GET",],
							AllowedOrigins =
							[
								"https://localhost:5001", "https://*.localhost:5001", "https://rocketexplorer.net",
								"https://*.rocketexplorer.net",
							],
							MaxAgeSeconds = 86400,
							ExposeHeaders = ["ETag",],
						},
					],
				},
			}, cancellationToken);
	}

	public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();

		await this.retryPolicy.ExecuteAsync(() =>
			this.s3Client.DeleteObjectAsync(
				new DeleteObjectRequest
				{
					BucketName = this.bucketName,
					Key = $"{this.options.Environment.ToLower()}/{key}",
				},
				cancellationToken));

		this.logger.LogDebug($"DeleteObject took {stopwatch.ElapsedMilliseconds}ms");
	}

	public async Task MigrateAsync<TOld, TNew>(
		string key, Func<TOld, TNew> migrator, CancellationToken cancellationToken = default)
		where TOld : class
		where TNew : class
	{
		BlobObject<TOld>? blobObject = await ReadAsync<TOld>(key, cancellationToken);

		if (blobObject == null)
		{
			throw new InvalidOperationException($"Snapshot '{key}' not found");
		}

		TNew migratedSnapshot = migrator(blobObject.Data);

		await WriteAsync(
			key, new BlobObject<TNew>
			{
				ProcessedBlockNumber = blobObject.ProcessedBlockNumber,
				Data = migratedSnapshot,
			}, cancellationToken: cancellationToken);
	}

	public async Task<BlobObject<T>?> ReadAsync<T>(string key, CancellationToken cancellationToken = default)
		where T : class
	{
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			using GetObjectResponse response = await this.retryPolicy.ExecuteAsync(
				innerCancellationToken => this.s3Client.GetObjectAsync(
					new GetObjectRequest
					{
						BucketName = this.bucketName,
						Key = $"{this.options.Environment.ToLower()}/{key}",
					},
					innerCancellationToken),
				cancellationToken);

			using MemoryStream memoryStream = new();
			await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
			memoryStream.Seek(0, SeekOrigin.Begin);

			this.logger.LogDebug($"GetObject took {stopwatch.ElapsedMilliseconds}ms for {memoryStream.Length} bytes");

			T data = MessagePackSerializer.Deserialize<T>(
				memoryStream.ToArray(),
				MessagePackSerializerOptions.Standard.WithResolver(this.messagePackResolver));

			return new BlobObject<T>
			{
				ProcessedBlockNumber = long.Parse(response.Metadata["ProcessedBlockNumber"]),
				Data = data,
			};
		}
		catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	public async Task WriteAsync<T>(
		string key, BlobObject<T> snapshot, int maxAge = 60, CancellationToken cancellationToken = default)
	{
		byte[] data = MessagePackSerializer.Serialize(
			snapshot.Data, MessagePackSerializerOptions.Standard.WithResolver(this.messagePackResolver));

		Stopwatch stopwatch = Stopwatch.StartNew();

		await this.retryPolicy.ExecuteAsync(
			async innerCancellationToken =>
			{
				await using MemoryStream memoryStream = new(data);

				await this.s3Client.PutObjectAsync(
					new PutObjectRequest
					{
						BucketName = this.bucketName,
						Key = $"{this.options.Environment.ToLower()}/{key}",
						InputStream = memoryStream,
						Headers =
						{
							["Cache-Control"] = $"public, max-age={maxAge}, must-revalidate",
						},
						Metadata =
						{
							["ProcessedBlockNumber"] =
								snapshot.ProcessedBlockNumber.ToString(CultureInfo.InvariantCulture),
						},
					},
					innerCancellationToken);
			}, cancellationToken);

		this.logger.LogDebug($"PutObject took {stopwatch.ElapsedMilliseconds}ms for {data.Length} bytes");
	}
}