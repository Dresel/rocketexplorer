using System.Numerics;
using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class TokensSnapshot
{
	[Key(0)]
	public required Token RPLOld { get; init; }

	[Key(1)]
	public required Token RPL { get; init; }

	[Key(2)]
	public required Token RETH { get; init; }

	[Key(3)]
	public required SortedList<DateOnly, BigInteger> RPLSwappedTotal { get; init; }

	[Key(4)]
	public required SortedList<DateOnly, BigInteger> RPLSwappedPerDay { get; init; }
}