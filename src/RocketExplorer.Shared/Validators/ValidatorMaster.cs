using MessagePack;

namespace RocketExplorer.Shared.Validators;

[MessagePackObject]
public record class ValidatorMaster
{
	[Key(0)]
	public byte[]? MegapoolAddress { get; init; }

	[Key(1)]
	public byte[]? MinipoolAddress { get; init; }

	[Key(2)]
	public required byte[]? PubKey { get; init; }

	[Key(3)]
	public required long? ValidatorIndex { get; init; }

	[Key(4)]
	public bool? ExpressTicketUsed { get; init; }

	[Key(5)]
	public int? MegapoolIndex { get; init; }

	[Key(6)]
	public ValidatorType Type { get; init; }

	// TODO: Bond? Average NETH for Megapool? getBondRequirement, hardcode?
	[Key(7)]
	public float Bond { get; init; }

	[Key(8)]
	public ValidatorStatus Status { get; init; }

	[Key(9)]
	public required ValidatorHistory[] History { get; init; }
}
