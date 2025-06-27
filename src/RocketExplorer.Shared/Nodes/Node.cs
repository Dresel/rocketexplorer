using MessagePack;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class Node
{
	[Key(0)]
	public required byte[] ContractAddress { get; init; }

	[Key(1)]
	public long RegistrationTimestamp { get; set; }

	[Key(2)]
	public byte[]? MegapoolAddress { get; init; }

	[Key(4)]
	public MinipoolValidatorIndexEntry[] MinipoolValidators { get; set; } = [];

	[Key(5)]
	public MegapoolValidatorIndexEntry[] MegapoolValidators { get; set; } = [];

	[Key(6)]
	public required string Timezone { get; set; }
}