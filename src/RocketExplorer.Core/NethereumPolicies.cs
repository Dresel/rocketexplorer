using System.Net;
using System.Net.Sockets;
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
		.Or<RpcClientUnknownException>(x =>
			(x.InnerException as HttpRequestException)?.StatusCode == HttpStatusCode.TooManyRequests)
		.Or<RpcClientUnknownException>(x =>
			x.InnerException?.InnerException is SocketException)
		.WaitAndRetryAsync(
			Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(30), 5),
			(exception, timeSpan, retryCount, _) =>
			{
				logger.LogInformation(
					$"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to {exception.Message}");
			});
}