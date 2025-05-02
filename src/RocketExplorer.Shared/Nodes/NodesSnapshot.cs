using MessagePack;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class NodesSnapshot
{
	[Key(0)]
	public long BlockHeight { get; init; }

	[Key(1)]
	public required NodeIndexEntry[] Index { get; init; }

	// TODO: Separate file?
	[Key(2)]
	public required SortedList<DateOnly, int> DailyRegistrations { get; init; }

	[Key(3)]
	public required SortedList<DateOnly, int> TotalNodeCount { get; init; }
}