using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Shared.Minipools;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Core.Nodes;

public class NodesSyncContext : ContextBase
{
	// TODO: What to persist?
	// Total megapool minipools count

	public SortedList<DateOnly, int> DailyDequeued { get; set; } = [];

	public SortedList<DateOnly, int> DailyEnqueued { get; set; } = [];

	public SortedList<DateOnly, int> DailyRegistrations { get; init; } = [];

	public SortedList<DateOnly, int> DailyVoluntaryExits { get; set; } = [];

	public List<MinipoolIndexEntry> ExpressQueue { get; init; } = [];

	public Dictionary<string, Dictionary<int, Minipool>> MegaMinipools { get; init; } =
		new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, string> MegapoolNodeOperatorMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, NodeIndexEntry> NodeIndex { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, Node> Nodes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public required RocketNodeManagerService RocketNodeManager { get; set; }

	public required string[] RocketNodeManagerAddresses { get; init; }

	public List<MinipoolIndexEntry> StandardQueue { get; init; } = [];

	public SortedList<DateOnly, int> TotalNodesCount { get; init; } = [];

	public SortedList<DateOnly, int> TotalQueueCount { get; set; } = [];
}