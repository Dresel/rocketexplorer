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