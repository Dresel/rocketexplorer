using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class TokensRockRETHSnapshot
{
	[Key(0)]
	public required Token? RockRETH { get; init; }
}

[MessagePackObject]
public record class TokensRockRETHSnapshotOld
{
	[Key(0)]
	public required TokenOld? RockRETH { get; init; }
}