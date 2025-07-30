using System.Numerics;

namespace RocketExplorer.Core.Tokens;

// TODO: Store transactions for mints, burns and transfers
public class TokenInfo
{
	public required Dictionary<string, BigInteger> Holders { get; init; }

	public required SortedList<DateOnly, BigInteger> MintsPerDay { get; init; }

	public required SortedList<DateOnly, BigInteger> BurnsPerDay { get; init; }

	public required SortedList<DateOnly, BigInteger> SupplyTotal { get; init; }
}