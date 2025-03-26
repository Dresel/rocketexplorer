using System.Collections.ObjectModel;
using Nethereum.Web3;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core;

public class ContextBase
{
	public required ReadOnlyDictionary<string, RocketPoolContract> Contracts { get; init; }

	public required long CurrentBlockHeight { get; set; }

	public required RocketStorageService RocketStorage { get; init; }

	public required Web3 Web3 { get; init; }
}