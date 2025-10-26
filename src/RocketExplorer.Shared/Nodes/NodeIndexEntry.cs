using MessagePack;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class NodeIndexEntry
{
	[Key(0)]
	public required long RegistrationTimestamp { get; init; }

	[Key(1)]
	public required byte[] ContractAddress { get; init; }

	[Key(2)]
	public string? ContractAddressEnsName { get; init; }

	[Key(3)]
	public byte[]? MegapoolAddress { get; init; }
}