using MessagePack;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class NodesSnapshot
{
	[Key(0)]
	public required NodeIndexEntry[] Index { get; init; }

	// TODO: Separate file?
	[Key(1)]
	public required SortedList<DateOnly, int> DailyRegistrations { get; init; }

	[Key(2)]
	public required SortedList<DateOnly, int> TotalNodeCount { get; init; }
}