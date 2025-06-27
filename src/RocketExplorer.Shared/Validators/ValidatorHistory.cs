using MessagePack;

namespace RocketExplorer.Shared.Validators;

[MessagePackObject]
public class ValidatorHistory
{
	[Key(0)]
	public required long Timestamp { get; init; }

	[Key(1)]
	public required ValidatorStatus Status { get; init; }
}