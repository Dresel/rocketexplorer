using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public class GlobalIndexShardSnapshot
{
	[Key(0)]
	public required IndexEntry[] Index { get; init; }
}