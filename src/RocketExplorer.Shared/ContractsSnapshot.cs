using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class ContractsSnapshot
{
	[Key(0)]
	public ulong BlockHeight { get; init; }

	[Key(1)]
	public RocketpoolContract[] Contracts { get; init; } = [];

	[Key(2)]
	public RocketpoolUpgradeContract[] UpgradeContracts { get; init; } = [];
}