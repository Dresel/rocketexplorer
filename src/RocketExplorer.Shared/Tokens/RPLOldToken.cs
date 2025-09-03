using System.Numerics;
using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class RPLOldToken : Token
{
	[Key(5)]
	public required SortedList<DateOnly, BigInteger> SwappedTotal { get; init; }

	[Key(6)]
	public required SortedList<DateOnly, BigInteger> SwappedDaily { get; init; }
}