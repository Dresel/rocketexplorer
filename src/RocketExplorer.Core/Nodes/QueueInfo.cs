using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public class QueueInfo
{
	public SortedList<DateOnly, int> DailyDequeued { get; set; } = [];

	public SortedList<DateOnly, int> DailyEnqueued { get; set; } = [];

	public SortedList<DateOnly, int> DailyVoluntaryExits { get; set; } = [];

	public List<MegapoolValidatorQueueEntry> MegapoolExpressQueue { get; init; } = [];

	public int MegapoolQueueIndex { get; set; }

	public List<MegapoolValidatorQueueEntry> MegapoolStandardQueue { get; init; } = [];

	public List<MinipoolValidatorQueueEntry> MinipoolFullQueue { get; init; } = [];

	public List<MinipoolValidatorQueueEntry> MinipoolHalfQueue { get; init; } = [];

	public List<MinipoolValidatorQueueEntry> MinipoolVariableQueue { get; init; } = [];

	public SortedList<DateOnly, int> TotalQueueCount { get; set; } = [];
}