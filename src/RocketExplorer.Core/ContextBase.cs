using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Nethereum.Web3;
using Polly.Retry;
using RocketExplorer.Core.BeaconChain;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core;

public record class ContextBase
{
	public required BeaconChainService BeaconChainService { get; init; }

	public required ReadOnlyDictionary<string, RocketPoolContract> Contracts { get; init; }

	public required long CurrentBlockHeight { get; set; }

	public required DashboardInfo DashboardInfo { get; init; }

	public required GlobalIndexService GlobalIndexService { get; set; }

	public required long LatestBlockHeight { get; init; }

	public required ILogger Logger { get; init; }

	public required AsyncRetryPolicy Policy { get; init; }

	public required RocketStorageService RocketStorage { get; init; }

	public required Storage Storage { get; init; }

	public required Web3 Web3 { get; init; }
}