using MessagePack;

namespace RocketExplorer.Shared.Validators;

[MessagePackObject]
public record class Validator
{
	[Key(0)]
	public required byte[] NodeAddress { get; init; }

	[Key(1)]
	public byte[]? MegapoolAddress { get; init; }

	// Legacy contract address of RocketMinipoolBase
	[Key(2)]
	public byte[]? MinipoolAddress { get; init; }

	[Key(3)]
	public required byte[]? PubKey { get; init; }

	[Key(4)]
	public required long? ValidatorIndex { get; init; }

	[Key(5)]
	public bool? ExpressTicketUsed { get; set; }

	[Key(6)]
	public int? MegapoolIndex { get; set; }

	[Key(7)]
	public ValidatorType Type { get; set; }

	// TODO: Bond? Average NETH for Megapool? getBondRequirement, hardcode?
	[Key(8)]
	public float Bond { get; set; }

	[Key(9)]
	public ValidatorStatus Status { get; set; }

	[Key(10)]
	public required ValidatorHistory[] History { get; set; }
}