using RocketExplorer.Ethereum.RocketMinipoolManager;
using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public class NodesSyncContext : ContextBase
{
	// TODO: What to persist?
	// Total megapool minipools count
	public SortedList<DateOnly, int> DailyRegistrations { get; init; } = [];

	public required Dictionary<string, NodeIndexEntry> NodeIndex { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, Node> Nodes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public required RocketNodeManagerService RocketNodeManager { get; init; }

	public required string[] RocketNodeManagerAddresses { get; init; }

	public SortedList<DateOnly, int> TotalNodesCount { get; init; } = [];

	public required QueueInfo QueueInfo { get; init; }

	public required ValidatorInfo ValidatorInfo { get; init; }

	public required RocketMinipoolManagerService RocketMinipoolManager { get; init; }
}

public class QueueInfo
{
	public SortedList<DateOnly, int> DailyVoluntaryExits { get; set; } = [];

	public SortedList<DateOnly, int> DailyDequeued { get; set; } = [];

	public SortedList<DateOnly, int> DailyEnqueued { get; set; } = [];

	public SortedList<DateOnly, int> TotalQueueCount { get; set; } = [];

	public List<ValidatorIndexEntry> ExpressQueue { get; init; } = [];

	public List<ValidatorIndexEntry> StandardQueue { get; init; } = [];
}

public class ValidatorInfo
{
	public required string[] RocketMinipoolManagerAddresses { get; init; }

	public required Dictionary<string, ValidatorIndexEntry> ValidatorIndex { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, Validator> Validators { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, Dictionary<int, Validator>> MegaMinipools { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, string> MegapoolNodeOperatorMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, string> MinipoolNodeOperatorMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}