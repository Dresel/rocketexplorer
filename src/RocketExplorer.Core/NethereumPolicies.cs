using System.Net;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace RocketExplorer.Core;

public static class NethereumPolicies
{
	public static AsyncRetryPolicy Retry(ILogger logger) => Policy
		.Handle<RpcClientTimeoutException>()
		.Or<RpcClientUnknownException>(
			x => (x.InnerException as HttpRequestException)?.StatusCode == HttpStatusCode.TooManyRequests)
		.WaitAndRetryAsync(
			Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(30), 5),
			(exception, timeSpan, retryCount, _) =>
			{
				logger.LogDebug($"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to {exception.Message}");
			});
}