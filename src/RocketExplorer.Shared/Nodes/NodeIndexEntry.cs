using MessagePack;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class NodeIndexEntry
{
	[Key(0)]
	public required ulong RegistrationTimestamp { get; init; }

	[Key(1)]
	public required byte[] ContractAddress { get; init; }
}