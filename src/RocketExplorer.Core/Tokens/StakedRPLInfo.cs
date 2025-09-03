using System.Numerics;

namespace RocketExplorer.Core.Tokens;

public class StakedRPLInfo
{
	public required SortedList<DateOnly, BigInteger> LegacyStakedDaily { get; init; }

	public required SortedList<DateOnly, BigInteger> LegacyStakedTotal { get; init; }

	public required SortedList<DateOnly, BigInteger> LegacyUnstakedDaily { get; init; }

	public required SortedList<DateOnly, BigInteger> MegapoolStakedDaily { get; init; }

	public required SortedList<DateOnly, BigInteger> MegapoolStakedTotal { get; init; }

	public required SortedList<DateOnly, BigInteger> MegapoolUnstakedDaily { get; init; }
}