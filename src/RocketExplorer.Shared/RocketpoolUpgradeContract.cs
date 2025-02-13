using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class RocketpoolUpgradeContract
{
	[Key(0)]
	public required string Name { get; init; }

	[Key(1)]
	public required IReadOnlyList<VersionedRocketpoolUpgradeContract> Versions { get; init; }
}