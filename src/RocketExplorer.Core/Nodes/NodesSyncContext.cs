using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public class NodesSyncContext : ContextBase
{
	// TODO: What to persist?
	// Total megapool minipools count
	public SortedList<DateOnly, int> DailyDequeued { get; set; } = [];

	public SortedList<DateOnly, int> DailyEnqueued { get; set; } = [];

	public SortedList<DateOnly, int> DailyRegistrations { get; init; } = [];

	public SortedList<DateOnly, int> DailyVoluntaryExits { get; set; } = [];

	public List<ValidatorIndexEntry> ExpressQueue { get; init; } = [];

	public Dictionary<string, Dictionary<int, Validator>> MegaMinipools { get; init; } =
		new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, string> MegapoolNodeOperatorMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public required Dictionary<string, NodeIndexEntry> NodeIndex { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public required Dictionary<string, ValidatorIndexEntry> ValidatorIndex { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, Node> Nodes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public required string[] RocketMinipoolManagerAddresses { get; init; }

	public required RocketNodeManagerService RocketNodeManager { get; set; }

	public required string[] RocketNodeManagerAddresses { get; init; }

	public List<ValidatorIndexEntry> StandardQueue { get; init; } = [];

	public SortedList<DateOnly, int> TotalNodesCount { get; init; } = [];

	public SortedList<DateOnly, int> TotalQueueCount { get; set; } = [];
}