using MessagePack;

namespace RocketExplorer.Shared.Contracts;

[MessagePackObject]
public record class RocketPoolContract
{
	[Key(0)]
	public required string Name { get; init; }

	[Key(1)]
	public required IReadOnlyList<VersionedRocketPoolContract> Versions { get; init; }
}