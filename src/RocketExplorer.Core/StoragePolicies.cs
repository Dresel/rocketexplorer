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
		.Or<AmazonServiceException>(ex => ex.StatusCode switch
		{
			>= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests => true,
			_ when ex.ErrorCode == "SlowDown" => true,
			_ => false,
		})
		.WaitAndRetryAsync(
			Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(5), 5),
			(exception, timeSpan, retryCount, _) =>
			{
				logger.LogDebug($"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to {exception.Message}");
			});
}