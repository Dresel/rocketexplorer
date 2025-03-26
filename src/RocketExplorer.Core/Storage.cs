using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using MessagePack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace RocketExplorer.Core;

public class Storage(IOptions<SyncOptions> options, AmazonS3Client s3Client)
{
	private readonly string bucketName = "rocketexplorer";

	private readonly SyncOptions options = options.Value;
	private readonly AmazonS3Client s3Client = s3Client;

	////await this.s3Client.PutCORSConfigurationAsync(
	////	new PutCORSConfigurationRequest
	////	{
	////		BucketName = this.bucketName,
	////		Configuration = new CORSConfiguration
	////		{
	////			Rules =
	////			[
	////				new CORSRule
	////				{
	////					AllowedHeaders = ["*",],
	////					AllowedMethods = ["HEAD", "GET",],
	////					AllowedOrigins = ["https://localhost:5001", "https://*.localhost:5001", "https://rocketexplorer.net", "https://*.rocketexplorer.net",],
	////					MaxAgeSeconds = 86400,
	////				},
	////			],
	////		},
	////	}, cancellationToken);

	public async Task<T?> ReadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
	{
		try
		{
			using GetObjectResponse response = await this.s3Client.GetObjectAsync(
				new GetObjectRequest
				{
					BucketName = this.bucketName, Key = $"{this.options.Environment.ToLower()}/{key}",
				},
				cancellationToken);

			using MemoryStream memoryStream = new();
			await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
			memoryStream.Seek(0, SeekOrigin.Begin);

			return MessagePackSerializer.Deserialize<T>(memoryStream.ToArray(), MessagePackSerializerOptions.Standard);
		}
		catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	public async Task WriteAsync<T>(string key, T snapshot, CancellationToken cancellationToken = default)
	{
		byte[] data = MessagePackSerializer.Serialize(snapshot, MessagePackSerializerOptions.Standard);
		using MemoryStream contractsMemoryStream = new(data);

		await this.s3Client.PutObjectAsync(
			new PutObjectRequest
			{
				BucketName = this.bucketName,
				Key = $"{this.options.Environment.ToLower()}/{key}",
				InputStream = contractsMemoryStream,
				Headers = { ["Cache-Control"] = "public, max-age=60, must-revalidate", },
			},
			cancellationToken);
	}
}