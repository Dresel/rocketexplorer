using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class RocketpoolContract
{
	[Key(0)]
	public required string Name { get; init; }

	[Key(1)]
	public required IReadOnlyList<VersionedRocketpoolContract> Versions { get; init; }
}