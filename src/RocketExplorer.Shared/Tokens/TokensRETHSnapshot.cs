using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class TokensRETHSnapshot
{
	[Key(0)]
	public required Token RETH { get; init; }
}