using MessagePack;

namespace RocketExplorer.Shared.Minipools;

// TODO: Add additional sortable fields
[MessagePackObject]
public record class MinipoolIndexEntry
{
	[Key(0)]
	public required ulong CreationTimestamp { get; init; }

	[Key(1)]
	public byte[]? MegapoolAddress { get; set; }

	[Key(2)]
	public int? MegapoolIndex { get; set; }

	// Legacy contract address of RocketMinipoolBase
	[Key(3)]
	public byte[]? ContractAddress { get; init; }

	[Key(4)]
	public required byte[] PubKey { get; init; }
}