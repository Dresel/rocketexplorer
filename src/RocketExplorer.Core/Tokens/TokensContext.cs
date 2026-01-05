using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Nethereum.Util;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Core.Tokens;

public record class TokensContext
{
	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public required string[] PostSaturn1RocketNodeStakingAddresses { get; set; }

	public required string[] PreSaturn1RocketNodeStakingAddresses { get; set; }

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required long RETHSnapshotBlockHeight { get; set; }

	public required string RETHTokenAddress { get; init; }

	public required TokenInfo RETHTokenInfo { get; init; }

	public required long RockRETHSnapshotBlockHeight { get; set; }

	public required string? RockRETHTokenAddress { get; set; }

	public required TokenInfo RockRETHTokenInfo { get; init; }

	public required long RPLOldSnapshotBlockHeight { get; set; }

	public required string RPLOldTokenAddress { get; init; }

	public required RPLOldTokenInfo RPLOldTokenInfo { get; init; }

	public required long RPLSnapshotBlockHeight { get; set; }

	public required string RPLTokenAddress { get; init; }

	public required TokenInfo RPLTokenInfo { get; init; }

	public required StakedRPLInfo StakedRPLInfo { get; set; }

	public required long StakedRPLSnapshotBlockHeight { get; set; }

	public static async Task<TokensContext> ReadAsync(
		Func<string, Task<long?>> findDeploymentBlock,
		Storage storage, ContractsContext contractsContext, SyncOptions syncOptions, ILogger<TokensContext> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading token snapshots");

		Task<BlobObject<TokensRPLOldSnapshot>?> readRPLOldTask =
			storage.ReadAsync<TokensRPLOldSnapshot>(Keys.TokensRPLOldSnapshot, cancellationToken);
		Task<BlobObject<TokensRPLSnapshot>?> readRPLTask =
			storage.ReadAsync<TokensRPLSnapshot>(Keys.TokensRPLSnapshot, cancellationToken);
		Task<BlobObject<TokensRETHSnapshot>?> readRETHTask =
			storage.ReadAsync<TokensRETHSnapshot>(Keys.TokensRETHSnapshot, cancellationToken);
		Task<BlobObject<StakedRPLSnapshot>?> readStakedRPLTask =
			storage.ReadAsync<StakedRPLSnapshot>(Keys.TokensStakedRPLSnapshot, cancellationToken);
		Task<BlobObject<TokensRockRETHSnapshot>?> readRockRETHTask =
			storage.ReadAsync<TokensRockRETHSnapshot>(Keys.TokensRockRETHSnapshot, cancellationToken);

		await Task.WhenAll(readRPLOldTask, readRPLTask, readRETHTask, readStakedRPLTask, readRockRETHTask);

		await contractsContext.IsFinished;

		ReadOnlyDictionary<string, RocketPoolContract> contracts = contractsContext.ContextContracts.AsReadOnly();

		string rplOldContractAddress = contracts["rocketTokenRPLFixedSupply"].Versions.Select(x => x.Address).Single();
		string rplContractAddress = contracts["rocketTokenRPL"].Versions.Select(x => x.Address).Single();
		string rethContractAddress = contracts["rocketTokenRETH"].Versions.Select(x => x.Address).Single();

		BlobObject<TokensRPLOldSnapshot> rplOldSnapshot =
			await readRPLOldTask ??
			new BlobObject<TokensRPLOldSnapshot>
			{
				ProcessedBlockNumber = await findDeploymentBlock(rplOldContractAddress) ??
					throw new InvalidOperationException("Deployment block not found"),
				Data = new TokensRPLOldSnapshot
				{
					RPLOld = new RPLOldToken
					{
						Address = rplOldContractAddress,
						Holders = [],
						SupplyTotal = [],
						MintsDaily = [],
						BurnsDaily = [],
						SwappedDaily = [],
						SwappedTotal = [],
					},
				},
			};

		BlobObject<TokensRPLSnapshot> rplSnapshot =
			await readRPLTask ??
			new BlobObject<TokensRPLSnapshot>
			{
				ProcessedBlockNumber = await findDeploymentBlock(rplContractAddress) ??
					throw new InvalidOperationException("Deployment block not found"),
				Data = new TokensRPLSnapshot
				{
					RPL = new Token
					{
						Address = rplContractAddress,
						Holders = [],
						SupplyTotal = [],
						MintsDaily = [],
						BurnsDaily = [],
					},
				},
			};

		BlobObject<TokensRETHSnapshot> rethSnapshot =
			await readRETHTask ??
			new BlobObject<TokensRETHSnapshot>
			{
				ProcessedBlockNumber = await findDeploymentBlock(rethContractAddress) ??
					throw new InvalidOperationException("Deployment block not found"),
				Data = new TokensRETHSnapshot
				{
					RETH = new Token
					{
						Address = rethContractAddress,
						Holders = [],
						SupplyTotal = [],
						MintsDaily = [],
						BurnsDaily = [],
					},
				},
			};

		BlobObject<StakedRPLSnapshot> stakedRPLSnapshot =
			await readStakedRPLTask ??
			new BlobObject<StakedRPLSnapshot>
			{
				ProcessedBlockNumber = await findDeploymentBlock(rplContractAddress) ??
					throw new InvalidOperationException("Deployment block not found"),
				Data = new StakedRPLSnapshot
				{
					LegacyStakedDaily = [],
					LegacyUnstakedDaily = [],
					LegacyStakedTotal = [],
					MegapoolStakedDaily = [],
					MegapoolUnstakedDaily = [],
					MegapoolStakedTotal = [],
				},
			};

		string? rockRETHTokenAddress = syncOptions.Environment.ToLower(CultureInfo.InvariantCulture) switch
		{
			"testnet" => "0x066b118c1A8012E6E5DFf4274c91A4B85F5f3CC2",
			"local-testnet" => "0x066b118c1A8012E6E5DFf4274c91A4B85F5f3CC2",
			"mainnet" => "0x936faCdf10c8c36294e7b9d28345255539d81bc7",
			"local-mainnet" => "0x936faCdf10c8c36294e7b9d28345255539d81bc7",
			_ => null,
		};

		BlobObject<TokensRockRETHSnapshot> rockRETHSnapshot =
			await readRockRETHTask ??
			new BlobObject<TokensRockRETHSnapshot>
			{
				ProcessedBlockNumber =
					rockRETHTokenAddress is null ? 0 : await findDeploymentBlock(rockRETHTokenAddress) ?? 0,
				Data = new TokensRockRETHSnapshot
				{
					RockRETH = rockRETHTokenAddress is null
						? null
						: new Token
						{
							Address = rockRETHTokenAddress,
							Holders = [],
							SupplyTotal = [],
							MintsDaily = [],
							BurnsDaily = [],
						},
				},
			};

		return new TokensContext
		{
			CurrentBlockHeight = Math.Max(
				0,
				new[]
				{
					rplOldSnapshot.ProcessedBlockNumber - 1, rplSnapshot.ProcessedBlockNumber - 1,
					rethSnapshot.ProcessedBlockNumber - 1, rockRETHSnapshot.ProcessedBlockNumber - 1,
				}.Min()),

			RPLOldSnapshotBlockHeight = rplOldSnapshot.ProcessedBlockNumber,
			RPLSnapshotBlockHeight = rplSnapshot.ProcessedBlockNumber,
			RETHSnapshotBlockHeight = rethSnapshot.ProcessedBlockNumber,
			RockRETHSnapshotBlockHeight = rockRETHSnapshot.ProcessedBlockNumber,
			StakedRPLSnapshotBlockHeight = stakedRPLSnapshot.ProcessedBlockNumber,

			PreSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"].Versions
				.Where(x => x.Version <= 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS,]).ToArray(),
			PostSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"].Versions
				.Where(x => x.Version > 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS,]).ToArray(),

			RETHTokenAddress = rethContractAddress,
			RPLOldTokenAddress = rplOldContractAddress,
			RPLTokenAddress = rplContractAddress,
			RockRETHTokenAddress = rockRETHTokenAddress,

			RPLOldTokenInfo = new RPLOldTokenInfo
			{
				Holders = new SortedDictionary<string, HolderEntry>(
					rplOldSnapshot.Data.RPLOld.Holders.Select(entry =>
						new KeyValuePair<string, HolderEntry>(entry.Address, entry)).ToDictionary(),
					StringComparer.OrdinalIgnoreCase),
				SupplyTotal = rplOldSnapshot.Data.RPLOld.SupplyTotal,
				MintsDaily = rplOldSnapshot.Data.RPLOld.MintsDaily,
				BurnsDaily = rplOldSnapshot.Data.RPLOld.BurnsDaily,
				SwappedTotal = rplOldSnapshot.Data.RPLOld.SwappedTotal,
				SwappedDaily = rplOldSnapshot.Data.RPLOld.SwappedDaily,
			},
			RPLTokenInfo = new TokenInfo
			{
				Holders = new SortedDictionary<string, HolderEntry>(
					rplSnapshot.Data.RPL.Holders.Select(entry =>
						new KeyValuePair<string, HolderEntry>(entry.Address, entry)).ToDictionary(),
					StringComparer.OrdinalIgnoreCase),
				SupplyTotal = rplSnapshot.Data.RPL.SupplyTotal,
				MintsDaily = rplSnapshot.Data.RPL.MintsDaily,
				BurnsDaily = rplSnapshot.Data.RPL.BurnsDaily,
			},
			RETHTokenInfo = new TokenInfo
			{
				Holders = new SortedDictionary<string, HolderEntry>(
					rethSnapshot.Data.RETH.Holders.Select(entry =>
						new KeyValuePair<string, HolderEntry>(entry.Address, entry)).ToDictionary(),
					StringComparer.OrdinalIgnoreCase),
				SupplyTotal = rethSnapshot.Data.RETH.SupplyTotal,
				MintsDaily = rethSnapshot.Data.RETH.MintsDaily,
				BurnsDaily = rethSnapshot.Data.RETH.BurnsDaily,
			},
			StakedRPLInfo = new StakedRPLInfo
			{
				LegacyStakedTotal = stakedRPLSnapshot.Data.LegacyStakedTotal,
				LegacyStakedDaily = stakedRPLSnapshot.Data.LegacyStakedDaily,
				LegacyUnstakedDaily = stakedRPLSnapshot.Data.LegacyUnstakedDaily,
				MegapoolStakedTotal = stakedRPLSnapshot.Data.MegapoolStakedTotal,
				MegapoolStakedDaily = stakedRPLSnapshot.Data.MegapoolStakedDaily,
				MegapoolUnstakedDaily = stakedRPLSnapshot.Data.MegapoolUnstakedDaily,
			},
			RockRETHTokenInfo = new TokenInfo
			{
				Holders = new SortedDictionary<string, HolderEntry>(
					rockRETHSnapshot.Data.RockRETH?.Holders.Select(entry =>
						new KeyValuePair<string, HolderEntry>(entry.Address, entry)).ToDictionary() ?? [],
					StringComparer.OrdinalIgnoreCase),
				SupplyTotal = rockRETHSnapshot.Data.RockRETH?.SupplyTotal ?? [],
				MintsDaily = rockRETHSnapshot.Data.RockRETH?.MintsDaily ?? [],
				BurnsDaily = rockRETHSnapshot.Data.RockRETH?.BurnsDaily ?? [],
			},
		};
	}

	public async Task SaveAsync(
		Storage storage, ILogger<TokensContext> logger, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.TokensRPLOldSnapshot);
		Task writeRplOldTask = storage.WriteAsync(
			Keys.TokensRPLOldSnapshot, new BlobObject<TokensRPLOldSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new TokensRPLOldSnapshot
				{
					RPLOld = new RPLOldToken
					{
						Address = RPLOldTokenAddress,
						Holders = RPLOldTokenInfo.Holders.Select(x => x.Value).ToArray(),
						SupplyTotal = RPLOldTokenInfo.SupplyTotal,
						MintsDaily = RPLOldTokenInfo.MintsDaily,
						BurnsDaily = RPLOldTokenInfo.BurnsDaily,
						SwappedTotal = RPLOldTokenInfo.SwappedTotal,
						SwappedDaily = RPLOldTokenInfo.SwappedDaily,
					},
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.TokensRPLSnapshot);
		Task writeRPLTask = storage.WriteAsync(
			Keys.TokensRPLSnapshot, new BlobObject<TokensRPLSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new TokensRPLSnapshot
				{
					RPL = new Token
					{
						Address = RPLTokenAddress,
						Holders = RPLTokenInfo.Holders.Select(x => x.Value).ToArray(),
						SupplyTotal = RPLTokenInfo.SupplyTotal,
						MintsDaily = RPLTokenInfo.MintsDaily,
						BurnsDaily = RPLTokenInfo.BurnsDaily,
					},
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.TokensRETHSnapshot);
		Task writeRETHTask = storage.WriteAsync(
			Keys.TokensRETHSnapshot, new BlobObject<TokensRETHSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new TokensRETHSnapshot
				{
					RETH = new Token
					{
						Address = RETHTokenAddress,
						Holders = RETHTokenInfo.Holders.Select(x => x.Value).ToArray(),
						SupplyTotal = RETHTokenInfo.SupplyTotal,
						MintsDaily = RETHTokenInfo.MintsDaily,
						BurnsDaily = RETHTokenInfo.BurnsDaily,
					},
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.TokensStakedRPLSnapshot);
		Task writeStakedRPLTask = storage.WriteAsync(
			Keys.TokensStakedRPLSnapshot, new BlobObject<StakedRPLSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new StakedRPLSnapshot
				{
					LegacyStakedTotal = StakedRPLInfo.LegacyStakedTotal,
					LegacyStakedDaily = StakedRPLInfo.LegacyStakedDaily,
					LegacyUnstakedDaily = StakedRPLInfo.LegacyUnstakedDaily,
					MegapoolStakedTotal = StakedRPLInfo.MegapoolStakedTotal,
					MegapoolStakedDaily = StakedRPLInfo.MegapoolStakedDaily,
					MegapoolUnstakedDaily = StakedRPLInfo.MegapoolUnstakedDaily,
				},
			}, cancellationToken: cancellationToken);

		logger.LogInformation("Writing {snapshot}", Keys.TokensRockRETHSnapshot);
		Task writeRockRETHTask = storage.WriteAsync(
			Keys.TokensRockRETHSnapshot, new BlobObject<TokensRockRETHSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new TokensRockRETHSnapshot
				{
					RockRETH = RockRETHTokenAddress is null
						? null
						: new Token
						{
							Address = RockRETHTokenAddress,
							Holders = RockRETHTokenInfo.Holders.Select(x => x.Value).ToArray(),
							SupplyTotal = RockRETHTokenInfo.SupplyTotal,
							MintsDaily = RockRETHTokenInfo.MintsDaily,
							BurnsDaily = RockRETHTokenInfo.BurnsDaily,
						},
				},
			}, cancellationToken: cancellationToken);

		await Task.WhenAll(writeRplOldTask, writeRPLTask, writeRETHTask, writeStakedRPLTask, writeRockRETHTask);
	}
}