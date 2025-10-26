using MessagePack;

namespace RocketExplorer.Shared.Validators;

[MessagePackObject]
public record class Validator
{
	[Key(0)]
	public required byte[] NodeAddress { get; init; }

	[Key(1)]
	public required string? NodeAddressEnsName { get; init; }

	[Key(2)]
	public byte[]? MegapoolAddress { get; init; }

	[Key(3)]
	public byte[]? MinipoolAddress { get; init; }

	[Key(4)]
	public required byte[]? PubKey { get; init; }

	[Key(5)]
	public required long? ValidatorIndex { get; init; }

	[Key(6)]
	public bool? ExpressTicketUsed { get; init; }

	[Key(7)]
	public int? MegapoolIndex { get; init; }

	[Key(8)]
	public ValidatorType Type { get; init; }

	// TODO: Bond? Average NETH for Megapool? getBondRequirement, hardcode?
	[Key(9)]
	public float Bond { get; init; }

	[Key(10)]
	public ValidatorStatus Status { get; init; }

	[Key(11)]
	public required ValidatorHistory[] History { get; init; }
}