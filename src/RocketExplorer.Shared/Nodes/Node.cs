using MessagePack;
using RocketExplorer.Shared.Minipools;

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

	[Key(3)]
	public MinipoolIndexEntry[] MegaMinipools { get; set; } = [];

	[Key(4)]
	public required string Timezone { get; set; }
}