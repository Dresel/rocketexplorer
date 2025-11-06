using MessagePack;

namespace RocketExplorer.Shared.Validators;

[MessagePackObject]
public record class MinipoolValidatorIndexEntry
{
	[Key(0)]
	public required byte[] NodeAddress { get; init; }

	[Key(1)]
	public required byte[] MinipoolAddress { get; init; }

	[Key(2)]
	public byte[]? PubKey { get; init; }

	[Key(3)]
	public long? ValidatorIndex { get; init; }
}