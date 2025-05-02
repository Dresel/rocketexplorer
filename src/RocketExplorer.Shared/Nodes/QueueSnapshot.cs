using MessagePack;
using RocketExplorer.Shared.Minipools;

namespace RocketExplorer.Shared.Nodes;

// TODO: Separate file for DateOnly dictionary structures? Distinguish between Standard and Express?
// TODO: Separate weekly, monthly values to save client-side computation?
[MessagePackObject]
public record class QueueSnapshot
{
	[Key(0)]
	public long BlockHeight { get; init; }

	[Key(1)]
	public required MinipoolIndexEntry[] StandardIndex { get; init; }

	[Key(2)]
	public required MinipoolIndexEntry[] ExpressIndex { get; init; }

	[Key(4)]
	public required SortedList<DateOnly, int> TotalQueueCount { get; init; }

	[Key(5)]
	public required Dictionary<DateOnly, int> DailyEnqueued { get; init; }

	[Key(6)]
	public required Dictionary<DateOnly, int> DailyDequeued { get; init; }

	[Key(7)]
	public required Dictionary<DateOnly, int> DailyVoluntaryExits { get; init; }
}