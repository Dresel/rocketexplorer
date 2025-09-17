using MessagePack;

namespace RocketExplorer.Shared.Validators;

[MessagePackObject]
public record class ValidatorSnapshot
{
	[Key(0)]
	public required MinipoolValidatorIndexEntry[] MinipoolValidatorIndex { get; init; }

	[Key(1)]
	public required MegapoolValidatorIndexEntry[] MegapoolValidatorIndex { get; init; }
}

[MessagePackObject]
public record class ValidatorSnapshot2
{
	[Key(0)]
	public required MinipoolValidatorIndexEntry2[] MinipoolValidatorIndex { get; init; }

	[Key(1)]
	public required MegapoolValidatorIndexEntry2[] MegapoolValidatorIndex { get; init; }
}