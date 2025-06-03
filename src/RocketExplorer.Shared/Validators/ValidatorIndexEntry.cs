using MessagePack;

namespace RocketExplorer.Shared.Validators;

// TODO: Add additional sortable fields
[MessagePackObject]
public record class ValidatorIndexEntry
{
	[Key(0)]
	public required byte[] NodeAddress { get; init; }

	[Key(1)]
	public byte[]? MegapoolAddress { get; init; }

	[Key(2)]
	public int? MegapoolIndex { get; set; }

	// Legacy contract address of RocketMinipoolBase
	[Key(3)]
	public byte[]? MinipoolAddress { get; init; }

	[Key(4)]
	public byte[]? PubKey { get; init; }
}