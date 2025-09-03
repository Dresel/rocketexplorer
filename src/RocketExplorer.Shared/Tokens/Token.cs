using System.Numerics;
using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class Token
{
	[Key(0)]
	[MessagePackFormatter(typeof(HexStringWithPrefixFormatter))]
	public required string Address { get; init; }

	[Key(1)]
	public required HolderEntry[] Holders { get; init; }

	[Key(2)]
	public required SortedList<DateOnly, BigInteger> SupplyTotal { get; init; }

	[Key(3)]
	public required SortedList<DateOnly, BigInteger> MintsDaily { get; init; }

	[Key(4)]
	public required SortedList<DateOnly, BigInteger> BurnsDaily { get; init; }
}