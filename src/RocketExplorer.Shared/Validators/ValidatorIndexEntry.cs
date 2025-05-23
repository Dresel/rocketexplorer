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

	// Legacy contract address of RocketMinipoolBase
	[Key(2)]
	public byte[]? MinipoolAddress { get; init; }

	[Key(3)]
	public required byte[] PubKey { get; init; }
}