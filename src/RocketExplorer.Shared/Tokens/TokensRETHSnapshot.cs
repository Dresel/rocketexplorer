using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class TokensRETHSnapshot
{
	[Key(0)]
	public required Token RETH { get; init; }
}

[MessagePackObject]
public record class TokensRETHSnapshot2
{
	[Key(0)]
	public required Token2 RETH { get; init; }
}