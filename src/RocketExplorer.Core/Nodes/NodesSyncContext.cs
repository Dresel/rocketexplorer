using RocketExplorer.Ethereum.RocketMinipoolManager;
using RocketExplorer.Ethereum.RocketNodeManager;

namespace RocketExplorer.Core.Nodes;

public class NodesSyncContext : ContextBase
{
	public required NodeInfo Nodes { get; init; }

	public required string[] PostSaturn1RocketNodeStakingAddresses { get; set; }

	public required string[] PreSaturn1RocketNodeStakingAddresses { get; set; }

	public required QueueInfo QueueInfo { get; init; }

	public required RocketMinipoolManagerService RocketMinipoolManager { get; init; }

	public required RocketNodeManagerService RocketNodeManager { get; init; }

	public required string[] RocketNodeManagerAddresses { get; init; }

	public required ValidatorInfo ValidatorInfo { get; init; }
}