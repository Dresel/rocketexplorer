using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace RocketExplorer.Core;

public static class StoragePolicies
{
	public static AsyncRetryPolicy Retry(ILogger logger) => Policy
		.Handle<IOException>()
		.Or<HttpRequestException>()
		.Or<SocketException>()
		.Or<TimeoutException>()
		.Or<TaskCanceledException>(exception => !exception.CancellationToken.IsCancellationRequested)
		.Or<AmazonServiceException>(exception => exception.StatusCode switch
		{
			>= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests => true,

			_ when exception.InnerException is IOException => true,
			_ when exception.InnerException is HttpRequestException => true,
			_ when exception.InnerException is SocketException => true,
			_ when exception.InnerException is TimeoutException => true,
			_ when exception.InnerException is TaskCanceledException { CancellationToken.IsCancellationRequested: false } => true,

			_ when exception.ErrorCode == "SlowDown" => true,
	_ => false,
		})
		.WaitAndRetryAsync(
			Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(5), 15),
			(exception, timeSpan, retryCount, _) =>
			{
				logger.LogInformation($"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to {exception.Message}");
			});
}