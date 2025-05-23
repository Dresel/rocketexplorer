using MessagePack;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Shared.Nodes;

// TODO: Separate file for DateOnly dictionary structures? Distinguish between Standard and Express?
// TODO: Separate weekly, monthly values to save client-side computation?
[MessagePackObject]
public record class QueueSnapshot
{
	[Key(0)]
	public required ValidatorIndexEntry[] StandardIndex { get; init; }

	[Key(1)]
	public required ValidatorIndexEntry[] ExpressIndex { get; init; }

	[Key(2)]
	public required SortedList<DateOnly, int> TotalQueueCount { get; init; }

	[Key(3)]
	public required SortedList<DateOnly, int> DailyEnqueued { get; init; }

	[Key(4)]
	public required SortedList<DateOnly, int> DailyDequeued { get; init; }

	[Key(5)]
	public required SortedList<DateOnly, int> DailyVoluntaryExits { get; init; }
}