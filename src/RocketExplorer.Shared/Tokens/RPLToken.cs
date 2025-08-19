using System.Numerics;
using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class RPLToken : Token
{
	[Key(4)]
	public required SortedList<DateOnly, BigInteger> LegacyStakedDaily { get; init; }

	[Key(5)]
	public required SortedList<DateOnly, BigInteger> LegacyUnstakedDaily { get; init; }

	[Key(6)]
	public required SortedList<DateOnly, BigInteger> LegacyStakedTotal { get; init; }

	[Key(7)]
	public required SortedList<DateOnly, BigInteger> MegapoolStakedDaily { get; init; }

	[Key(8)]
	public required SortedList<DateOnly, BigInteger> MegapoolUnstakedDaily { get; init; }

	[Key(9)]
	public required SortedList<DateOnly, BigInteger> MegapoolStakedTotal { get; init; }
}