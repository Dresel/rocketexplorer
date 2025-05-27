using MessagePack;

namespace RocketExplorer.Shared.Validators;

[MessagePackObject]
public record class ValidatorSnapshot
{
	[Key(0)]
	public required ValidatorIndexEntry[] Index { get; init; }
}