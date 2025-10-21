using RocketExplorer.Core;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Bootstrap;

internal class Migrator
{
	public static async Task MigrateAsync(Storage storage, CancellationToken cancellationToken = default)
	{
		await storage.MigrateAsync<NodesSnapshotOld, NodesSnapshot>(
			Keys.NodesSnapshot, snapshotOld => new NodesSnapshot
			{
				Index = snapshotOld.Index.Select(x => new NodeIndexEntry
				{
					ContractAddress = x.ContractAddress,
					MegapoolAddress = x.MegapoolAddress,
					RegistrationTimestamp = x.RegistrationTimestamp,
					ContractAddressEnsName = null,
				}).ToArray(),
				DailyRegistrations = snapshotOld.DailyRegistrations,
				TotalNodeCount = snapshotOld.TotalNodeCount,
			}, cancellationToken);

		await storage.MigrateAsync<TokensRETHSnapshotOld, TokensRETHSnapshot>(
			Keys.TokensRETHSnapshot, x => new TokensRETHSnapshot
			{
				RETH = MigrateTokenInfo(x.RETH),
			}, cancellationToken);

		await storage.MigrateAsync<TokensRockRETHSnapshotOld, TokensRockRETHSnapshot>(
			Keys.TokensRockRETHSnapshot, x => new TokensRockRETHSnapshot
			{
				RockRETH = x.RockRETH is null ? null : MigrateTokenInfo(x.RockRETH),
			}, cancellationToken);

		await storage.MigrateAsync<TokensRPLSnapshotOld, TokensRPLSnapshot>(
			Keys.TokensRPLSnapshot, x => new TokensRPLSnapshot
			{
				RPL = MigrateTokenInfo(x.RPL),
			}, cancellationToken);

		await storage.MigrateAsync<TokensRPLOldSnapshotOld, TokensRPLOldSnapshot>(
			Keys.TokensRPLOldSnapshot, x => new TokensRPLOldSnapshot
			{
				RPLOld = MigrateRPLOldTokenInfo(x.RPLOld),
			}, cancellationToken);
	}

	private static RPLOldToken MigrateRPLOldTokenInfo(RPLOldTokenOld old) =>
		new()
		{
			Address = old.Address,
			Holders = old.Holders.Select(x => new HolderEntry
			{
				Address = x.Address,
				AddressEnsName = null,
				Balance = x.Balance,
			}).ToArray(),
			MintsDaily = old.MintsDaily,
			BurnsDaily = old.BurnsDaily,
			SupplyTotal = old.SupplyTotal,
			SwappedDaily = old.SwappedDaily,
			SwappedTotal = old.SwappedTotal,
		};

	private static Token MigrateTokenInfo(TokenOld old) =>
		new()
		{
			Address = old.Address,
			Holders = old.Holders.Select(x => new HolderEntry
			{
				Address = x.Address,
				AddressEnsName = null,
				Balance = x.Balance,
			}).ToArray(),
			MintsDaily = old.MintsDaily,
			BurnsDaily = old.BurnsDaily,
			SupplyTotal = old.SupplyTotal,
		};
}