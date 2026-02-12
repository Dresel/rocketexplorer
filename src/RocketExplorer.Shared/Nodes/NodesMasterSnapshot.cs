using MessagePack;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class NodesMasterSnapshot
{
	[Key(0)]
	public required NodeMaster[] Nodes { get; init; }

	[Key(1)]
	public required SortedList<DateOnly, int> DailyRegistrations { get; init; }

	[Key(2)] 
	public required SortedList<DateOnly, int> TotalNodeCount { get; init; }

	[Key(3)]
	public required MinipoolValidatorQueueEntry[] MinipoolHalfQueue { get; init; }

	[Key(4)]
	public required MinipoolValidatorQueueEntry[] MinipoolFullQueue { get; init; }

	[Key(5)]
	public required MinipoolValidatorQueueEntry[] MinipoolVariableQueue { get; init; }

	[Key(6)]
	public required int MegapoolQueueIndex { get; init; }

	[Key(7)]
	public required MegapoolValidatorQueueEntry[] MegapoolStandardQueue { get; init; }

	[Key(8)]
	public required MegapoolValidatorQueueEntry[] MegapoolExpressQueue { get; init; }

	[Key(9)]
	public required SortedList<DateOnly, int> TotalQueueCount { get; init; }

	[Key(10)]
	public required SortedList<DateOnly, int> DailyEnqueued { get; init; }

	[Key(11)]
	public required SortedList<DateOnly, int> DailyDequeued { get; init; }

	[Key(12)]
	public required SortedList<DateOnly, int> DailyVoluntaryExits { get; init; }
}
