using MessagePack;

namespace RocketExplorer.Shared.Contracts;

[MessagePackObject]
public record class ContractsSnapshot
{
	[Key(0)]
	public required RocketPoolContract[] Contracts { get; init; }

	[Key(1)]
	public required RocketPoolUpgradeContract[] UpgradeContracts { get; init; }
}