using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class TokensRPLOldSnapshot
{
	[Key(0)]
	public required RPLOldToken RPLOld { get; init; }
}

[MessagePackObject]
public record class TokensRPLOldSnapshot2
{
	[Key(0)]
	public required RPLOldToken2 RPLOld { get; init; }
}