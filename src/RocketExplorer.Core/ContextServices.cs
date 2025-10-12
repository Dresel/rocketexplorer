using Nethereum.Web3;
using RocketExplorer.Core.BeaconChain;
using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Ethereum.RocketStorage;

namespace RocketExplorer.Core;

public record class ContextServices
{
	public required BeaconChainService BeaconChainService { get; init; }

	public required GlobalIndexService GlobalIndexService { get; init; }

	public required RocketNodeManagerService RocketNodeManager { get; init; }

	public required RocketStorageService RocketStorage { get; init; }

	public required Storage Storage { get; init; }

	public required Web3 Web3 { get; init; }
}