using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class ContractsSnapshot
{
	[Key(0)]
	public ulong BlockHeight { get; init; }

	[Key(1)]
	public RocketPoolContract[] Contracts { get; init; } = [];

	[Key(2)]
	public RocketPoolUpgradeContract[] UpgradeContracts { get; init; } = [];
}