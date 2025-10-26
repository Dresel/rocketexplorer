using System.Numerics;
using MessagePack;

namespace RocketExplorer.Shared.Tokens;

[MessagePackObject]
public record class HolderEntry
{
	[Key(0)]
	[MessagePackFormatter(typeof(HexStringWithPrefixFormatter))]
	public required string Address { get; init; }

	[Key(1)]
	public required string? AddressEnsName { get; init; }

	[Key(2)]
	public required BigInteger Balance { get; init; }
}