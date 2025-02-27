using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class RocketPoolUpgradeContract
{
	[Key(0)]
	public required string Name { get; init; }

	[Key(1)]
	public required IReadOnlyList<VersionedRocketPoolUpgradeContract> Versions { get; init; }
}