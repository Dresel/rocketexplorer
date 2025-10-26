using System.Numerics;
using MessagePack;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class Node
{
	[Key(0)]
	public required byte[] ContractAddress { get; init; }

	[Key(1)]
	public string? ContractAddressEns { get; set; }

	[Key(2)]
	public long RegistrationTimestamp { get; set; }

	[Key(3)]
	public byte[]? MegapoolAddress { get; init; }

	[Key(4)]
	public MinipoolValidatorIndexEntry[] MinipoolValidators { get; set; } = [];

	[Key(5)]
	public MegapoolValidatorIndexEntry[] MegapoolValidators { get; set; } = [];

	[Key(6)]
	public required string Timezone { get; set; }

	[Key(7)]
	public BigInteger RPLLegacyStaked { get; set; } = 0;

	[Key(8)]
	public BigInteger RPLMegapoolStaked { get; set; } = 0;
}