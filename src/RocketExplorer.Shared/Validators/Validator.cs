using MessagePack;

namespace RocketExplorer.Shared.Validators;

[MessagePackObject]
public class Validator
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

	[Key(4)]
	public bool? ExpressTicketUsed { get; set; }

	[Key(5)]
	public int? MegapoolIndex { get; set; }

	[Key(6)]
	public ValidatorType Type { get; set; }

	// TODO: Bond? Average NETH for Megapool? getBondRequirement, hardcode?
	[Key(7)]
	public byte Bond { get; set; }

	[Key(8)]
	public ValidatorStatus Status { get; set; }

	[Key(9)]
	public ulong CreationTimestamp { get; set; }

	[Key(10)]
	public ulong? EnqueueTimestamp { get; set; }

	// Voluntarily exited or dequeued
	[Key(11)]
	public ulong? DequeueTimestamp { get; set; }

	[Key(12)]
	public ulong? DissolvedTimestamp { get; set; }

	[Key(13)]
	public ulong? StakingTimestamp { get; set; }

	[Key(14)]
	public ulong? ExitedTimestamp { get; set; }
}