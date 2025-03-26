using MessagePack;

namespace RocketExplorer.Shared.Contracts;

[MessagePackObject]
public record class VersionedRocketPoolUpgradeContract
{
	[Key(0)]
	public required long ActivationHeight { get; set; }

	[Key(1)]
	public required string ActivationMethod { get; set; }

	[Key(2)]
	public required string Address { get; init; }

	[Key(3)]
	public required bool IsExecuted { get; set; }

	[Key(4)]
	public long? ExecutionHeight { get; set; }
}