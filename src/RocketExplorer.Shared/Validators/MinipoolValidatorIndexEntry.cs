using MessagePack;

namespace RocketExplorer.Shared.Validators;

// TODO: Add additional sortable fields
[MessagePackObject]
public record class MinipoolValidatorIndexEntry
{
	[Key(0)]
	public required byte[] NodeAddress { get; init; }

	// Legacy contract address of RocketMinipoolBase
	[Key(1)]
	public required byte[] MinipoolAddress { get; init; }

	[Key(2)]
	public byte[]? PubKey { get; init; }
}

[MessagePackObject]
public record class MegapoolValidatorIndexEntry
{
	[Key(0)]
	public required byte[] NodeAddress { get; init; }

	[Key(1)]
	public required byte[] MegapoolAddress { get; init; }

	[Key(2)]
	public required int MegapoolIndex { get; set; }

	[Key(3)]
	public byte[] PubKey { get; init; }
}