using System.Numerics;
using Microsoft.Extensions.Logging;
using RocketExplorer.Shared;

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

	public static async Task<DashboardInfo> ReadAsync(
		Storage storage, ILogger<DashboardInfo> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading {snapshot}", Keys.DashboardSnapshot);
		BlobObject<DashboardSnapshot> dashboardSnapshot =
			await storage.ReadAsync<DashboardSnapshot>(Keys.DashboardSnapshot, cancellationToken) ??
			new BlobObject<DashboardSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new DashboardSnapshot
				{
					RPLOldTotalSupply = 0,
					RPLTotalSupply = 0,
					RETHTotalSupply = 0,
					RPLSwapped = 0,
					RPLLegacyStakedTotal = 0,
					RPLMegapoolStakedTotal = 0,
					NodeOperators = 0,
					MinipoolValidatorsStaking = 0,
					MegapoolValidatorsStaking = 0,
					QueueLength = 0,
				},
			};

		return new DashboardInfo
		{
			RPLOldSupplyTotal = dashboardSnapshot.Data.RPLOldTotalSupply,
			RPLSupplyTotal = dashboardSnapshot.Data.RPLTotalSupply,
			RETHSupplyTotal = dashboardSnapshot.Data.RETHTotalSupply,
			RPLSwappedTotal = dashboardSnapshot.Data.RPLSwapped,
			NodeOperators = dashboardSnapshot.Data.NodeOperators,
			MinipoolValidatorsStaking = dashboardSnapshot.Data.MinipoolValidatorsStaking,
			MegapoolValidatorsStaking = dashboardSnapshot.Data.MegapoolValidatorsStaking,
			QueueLength = dashboardSnapshot.Data.QueueLength,
			RPLLegacyStakedTotal = dashboardSnapshot.Data.RPLLegacyStakedTotal,
			RPLMegapoolStakedTotal = dashboardSnapshot.Data.RPLMegapoolStakedTotal,
			RockRETHSupplyTotal = dashboardSnapshot.Data.RockRETHTotalSupply,
		};
	}

	public async Task SaveAsync(
		Storage storage, long processedBlockNumber, ILogger<DashboardInfo> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.DashboardSnapshot);
		await storage.WriteAsync(
			Keys.DashboardSnapshot,
			new BlobObject<DashboardSnapshot>
			{
				ProcessedBlockNumber = processedBlockNumber,
				Data = new DashboardSnapshot
				{
					RPLOldTotalSupply = RPLOldSupplyTotal,
					RPLTotalSupply = RPLSupplyTotal,
					RETHTotalSupply = RETHSupplyTotal,
					RPLSwapped = RPLSwappedTotal,
					RPLLegacyStakedTotal = RPLLegacyStakedTotal,
					RPLMegapoolStakedTotal = RPLMegapoolStakedTotal,
					NodeOperators = NodeOperators,
					MinipoolValidatorsStaking = MinipoolValidatorsStaking,
					MegapoolValidatorsStaking = MegapoolValidatorsStaking,
					QueueLength = QueueLength,
				},
			}, cancellationToken: cancellationToken);
	}
}