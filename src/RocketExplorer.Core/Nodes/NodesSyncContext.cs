using Microsoft.Extensions.Logging;
using Polly;
using RocketExplorer.Ethereum.RocketMinipoolManager;
using RocketExplorer.Ethereum.RocketNodeManager;

namespace RocketExplorer.Core.Nodes;

public class NodesSyncContext : ContextBase
{
	public required DashboardInfo DashboardInfo { get; init; }

	public required ILogger<NodesSyncContext> Logger { get; init; }

	public required NodeInfo Nodes { get; init; }

	public required AsyncPolicy Policy { get; set; }

	public required QueueInfo QueueInfo { get; init; }

	public required RocketMinipoolManagerService RocketMinipoolManager { get; init; }

	public required RocketNodeManagerService RocketNodeManager { get; init; }

	public required string[] RocketNodeManagerAddresses { get; init; }

	public required Storage Storage { get; set; }

	public required ValidatorInfo ValidatorInfo { get; init; }
}