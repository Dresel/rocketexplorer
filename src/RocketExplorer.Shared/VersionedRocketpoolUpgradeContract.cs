using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class VersionedRocketpoolUpgradeContract
{
	[Key(0)]
	public required ulong ActivationHeight { get; set; }

	[Key(1)]
	public required string ActivationMethod { get; set; }

	[Key(2)]
	public required string Address { get; init; }

	[Key(3)]
	public required bool IsExecuted { get; set; }

	[Key(4)]
	public ulong? ExecutionHeight { get; set; }
}