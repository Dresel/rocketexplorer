using MessagePack;

namespace RocketExplorer.Shared.Minipools;

// TODO: Add additional sortable fields
[MessagePackObject]
public record class MinipoolIndexEntry
{
	[Key(0)]
	public required long CreationTimestamp { get; init; }

	[Key(1)]
	public required byte[] NodeAddress { get; init; }

	[Key(2)]
	public byte[]? MegapoolAddress { get; init; }

	[Key(3)]
	public int? MegapoolIndex { get; init; }

	// Legacy contract address of RocketMinipoolBase
	[Key(4)]
	public byte[]? ContractAddress { get; init; }

	[Key(5)]
	public required byte[] PubKey { get; init; }
}