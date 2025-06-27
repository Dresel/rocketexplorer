using MessagePack;

namespace RocketExplorer.Shared.Validators;

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
	public required byte[] PubKey { get; init; }
}