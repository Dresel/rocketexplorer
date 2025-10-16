using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public class GlobalIndexShardSnapshot<TEntry>
{
	[Key(0)]
	public required TEntry[] Index { get; init; }
}