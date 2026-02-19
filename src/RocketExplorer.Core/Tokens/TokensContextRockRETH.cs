using System.Globalization;
using Microsoft.Extensions.Logging;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Core.Tokens;

public record class TokensContextRockRETH
{
	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required string? RockRETHTokenAddress { get; set; }

	public required TokenInfo RockRETHTokenInfo { get; init; }

	public static async Task<TokensContextRockRETH> ReadAsync(
		Func<string, Task<long?>> findDeploymentBlock,
		Storage storage, ContractsContext contractsContext, SyncOptions syncOptions,
		ILogger<TokensContextRockRETH> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading {snapshot}", Keys.TokensRockRETHSnapshot);

		Task<BlobObject<TokensRockRETHSnapshot>?> readRockRETHTask =
			storage.ReadAsync<TokensRockRETHSnapshot>(Keys.TokensRockRETHSnapshot, cancellationToken);

		await contractsContext.IsFinished;

		string? rockRETHTokenAddress = syncOptions.Environment.ToLower(CultureInfo.InvariantCulture) switch
		{
			"testnet" => "0x066b118c1A8012E6E5DFf4274c91A4B85F5f3CC2",
			"local-testnet" => "0x066b118c1A8012E6E5DFf4274c91A4B85F5f3CC2",
			"mainnet" => "0x936faCdf10c8c36294e7b9d28345255539d81bc7",
			"local-mainnet" => "0x936faCdf10c8c36294e7b9d28345255539d81bc7",
			"local-mainnet4" => "0x936faCdf10c8c36294e7b9d28345255539d81bc7",
			_ => null,
		};

		BlobObject<TokensRockRETHSnapshot> rockRETHSnapshot =
			await readRockRETHTask ??
			new BlobObject<TokensRockRETHSnapshot>
			{
				ProcessedBlockNumber = rockRETHTokenAddress is null ? 0 : await findDeploymentBlock(rockRETHTokenAddress) - 1 ?? 0,
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

		return new TokensContextRockRETH
		{
			CurrentBlockHeight = rockRETHSnapshot.ProcessedBlockNumber,

			RockRETHTokenAddress = rockRETHTokenAddress,
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
		Storage storage, ILogger<TokensContextRockRETH> logger, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.TokensRockRETHSnapshot);
		await storage.WriteAsync(
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
	}
}