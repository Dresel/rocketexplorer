using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Nethereum.Web3;
using Polly;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core;

public class ContextBase
{
	public required ReadOnlyDictionary<string, RocketPoolContract> Contracts { get; init; }

	public required long CurrentBlockHeight { get; set; }

	public required DashboardInfo DashboardInfo { get; init; }

	public required ILogger Logger { get; init; }

	public required AsyncPolicy Policy { get; set; }

	public required RocketStorageService RocketStorage { get; init; }

	public required Storage Storage { get; set; }

	public required Web3 Web3 { get; init; }
}