namespace RocketExplorer.Core.Nodes;

public class NodesMasterState
{
	public required NodesMasterStateFull Data { get; init; }

	public HashSet<string> NodesUpdated { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	public HashSet<(string NodeAddress, string MinipoolAddress)> MinipoolValidatorsUpdated { get; init; } = new(new MinipoolValidatorUpdatedEqualityComparer());

	public HashSet<(string NodeAddress, string MegapoolAddress, int MegapoolIndex)> MegapoolValidatorsUpdated { get; init; } = new(new MegapoolValidatorUpdatedEqualityComparer());

	public class NodesMasterStateFull
	{
		public required OrderedDictionary<string, NodeMasterInfo> Nodes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		public required SortedList<DateOnly, int> DailyRegistrations { get; init; } = [];

		public required SortedList<DateOnly, int> TotalNodesCount { get; init; } = [];

		public Dictionary<string, string> MinipoolNodeAddresses { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		public Dictionary<string, string> MegapoolNodeAddresses { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	}
}
