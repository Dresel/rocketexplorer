using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Core.Nodes;

public class NodeInfo
{
	public required NodeInfoFull Data { get; init; }

	public NodeInfoPartial Partial { get; init; } = new();

	public class NodeInfoPartial
	{
		public Dictionary<string, Node> Updated { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	}

	public class NodeInfoFull
	{
		public required OrderedDictionary<string, NodeIndexEntry> Index { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		public required SortedList<DateOnly, int> DailyRegistrations { get; init; } = [];

		public required SortedList<DateOnly, int> TotalNodesCount { get; init; } = [];

		public required Dictionary<string, string> WithdrawalAddresses { get; set; }

		public required Dictionary<string, string> RPLWithdrawalAddresses { get; set; }

		public required Dictionary<string, HashSet<string>> StakeOnBehalfAddresses { get; set; }
	}
}