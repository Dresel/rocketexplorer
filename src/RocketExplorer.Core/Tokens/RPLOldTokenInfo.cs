using System.Numerics;

namespace RocketExplorer.Core.Tokens;

public class RPLOldTokenInfo : TokenInfo
{
	public required SortedList<DateOnly, BigInteger> SwappedDaily { get; init; }

	public required SortedList<DateOnly, BigInteger> SwappedTotal { get; init; }
}