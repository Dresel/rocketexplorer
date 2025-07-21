namespace RocketExplorer.Core.Nodes;

public class DashboardInfo
{
	public required int MegapoolValidatorsStaking { get; set; }

	public required int MinipoolValidatorsStaking { get; set; }

	public required int NodeOperators { get; set; }

	public required int QueueLength { get; set; }
}