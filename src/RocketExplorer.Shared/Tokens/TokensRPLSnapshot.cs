using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class TokensRPLSnapshot
{
	[Key(0)]
	public required Token RPL { get; init; }
}

[MessagePackObject]
public record class TokensRPLSnapshot2
{
	[Key(0)]
	public required Token2 RPL { get; init; }
}