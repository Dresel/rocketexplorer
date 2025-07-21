using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class DashboardSnapshot
{
	[Key(2)]
	public required int MegapoolValidatorsStaking { get; init; }

	[Key(1)]
	public required int MinipoolValidatorsStaking { get; init; }

	[Key(0)]
	public required int NodeOperators { get; init; }

	[Key(3)]
	public required int QueueLength { get; init; }
}