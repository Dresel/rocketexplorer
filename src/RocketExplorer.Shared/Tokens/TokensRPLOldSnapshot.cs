using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class TokensRPLOldSnapshot
{
	[Key(0)]
	public required RPLOldToken RPLOld { get; init; }
}