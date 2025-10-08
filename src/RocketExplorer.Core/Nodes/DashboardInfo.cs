using System.Numerics;

namespace RocketExplorer.Core.Nodes;

public class DashboardInfo
{
	public required int MegapoolValidatorsStaking { get; set; }

	public required int MinipoolValidatorsStaking { get; set; }

	public required int NodeOperators { get; set; }

	public required int QueueLength { get; set; }

	public required BigInteger RETHSupplyTotal { get; set; }

	public required BigInteger RockRETHSupplyTotal { get; set; }

	public required BigInteger RPLLegacyStakedTotal { get; set; }

	public required BigInteger RPLMegapoolStakedTotal { get; set; }

	public required BigInteger RPLOldSupplyTotal { get; set; }

	public required BigInteger RPLSupplyTotal { get; set; }

	public required BigInteger RPLSwappedTotal { get; set; }
}