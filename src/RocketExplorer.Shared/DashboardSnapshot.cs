using System.Numerics;
using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class DashboardSnapshot
{
	[Key(0)]
	public required BigInteger RPLOldTotalSupply { get; set; }

	[Key(1)]
	public required BigInteger RPLTotalSupply { get; set; }

	[Key(2)]
	public required BigInteger RETHTotalSupply { get; set; }

	[Key(3)]
	public required BigInteger RPLSwapped { get; set; }

	[Key(4)]
	public required BigInteger RPLLegacyStakedTotal { get; init; }

	[Key(5)]
	public required BigInteger RPLMegapoolStakedTotal { get; init; }

	[Key(6)]
	public required int NodeOperators { get; init; }

	[Key(7)]
	public required int MinipoolValidatorsStaking { get; init; }

	[Key(8)]
	public required int MegapoolValidatorsStaking { get; init; }

	[Key(9)]
	public required int QueueLength { get; init; }

	[Key(10)]
	public BigInteger RockRETHTotalSupply { get; set; }
}