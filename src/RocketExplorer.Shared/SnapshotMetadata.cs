using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public class SnapshotMetadata
{
	[Key(0)]
	public required long BlockNumber { get; init; }

	[Key(1)]
	public required long Timestamp { get; init; }
}