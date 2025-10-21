using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class TokensRETHSnapshot
{
	[Key(0)]
	public required Token RETH { get; init; }
}

[MessagePackObject]
public record class TokensRETHSnapshotOld
{
	[Key(0)]
	public required TokenOld RETH { get; init; }
}