using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public class QueueInfo
{
	public SortedList<DateOnly, int> DailyVoluntaryExits { get; set; } = [];

	public SortedList<DateOnly, int> DailyDequeued { get; set; } = [];

	public SortedList<DateOnly, int> DailyEnqueued { get; set; } = [];

	public SortedList<DateOnly, int> TotalQueueCount { get; set; } = [];

	public List<MegapoolValidatorIndexEntry> ExpressQueue { get; init; } = [];

	public List<MegapoolValidatorIndexEntry> StandardQueue { get; init; } = [];
}