using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class VersionedRocketpoolContract
{
	[Key(0)]
	public required ulong ActivationHeight { get; set; }

	[Key(1)]
	public required string ActivationMethod { get; set; }

	[Key(2)]
	public required string Address { get; init; }
}