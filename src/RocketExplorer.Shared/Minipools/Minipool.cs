using MessagePack;

namespace RocketExplorer.Shared.Minipools;

[MessagePackObject]
public class Minipool
{
	// TODO: More timestamps
	[Key(0)]
	public ulong CreationTimestamp { get; set; }

	[Key(1)]
	public required byte[] PubKey { get; set; }

	[Key(2)]
	public required byte[] NodeOperatorAddress { get; set; }

	[Key(3)]
	public byte[]? MegapoolAddress { get; set; }

	[Key(4)]
	public int? MegapoolIndex { get; set; }

	[Key(5)]
	public bool? ExpressTicketUsed { get; set; }

	// TODO: PubKey

	// TODO: Legacy Minipool Address (deployed contract)
	// TODO: Megapool Index

	// Legacy contract address of RocketMinipoolBase
	[Key(6)]
	public byte[]? ContractAddress { get; init; }

	[Key(7)]
	public MinipoolType Type { get; set; }

	// TODO: Bond? Average NETH for Megapool? getBondRequirement, hardcode?
	[Key(8)]
	public byte Bond { get; set; }

	[Key(9)]
	public MinipoolStatus Status { get; set; }
}