using System.Numerics;
using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class StakedRPLSnapshot
{
	[Key(0)]
	public required SortedList<DateOnly, BigInteger> LegacyStakedDaily { get; init; }

	[Key(1)]
	public required SortedList<DateOnly, BigInteger> LegacyUnstakedDaily { get; init; }

	[Key(2)]
	public required SortedList<DateOnly, BigInteger> LegacyStakedTotal { get; init; }

	[Key(3)]
	public required SortedList<DateOnly, BigInteger> MegapoolStakedDaily { get; init; }

	[Key(4)]
	public required SortedList<DateOnly, BigInteger> MegapoolUnstakedDaily { get; init; }

	[Key(5)]
	public required SortedList<DateOnly, BigInteger> MegapoolStakedTotal { get; init; }
}