using System.Numerics;
using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class HolderEntry
{
	[Key(0)]
	public required byte[] Address { get; init; }

	[Key(1)]
	public required BigInteger Balance { get; init; }
}