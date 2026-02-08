using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Core.Tokens;

public record class TokensContextRPLOld
{
	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required string RPLTokenAddress { get; init; }

	public required string RPLOldTokenAddress { get; init; }

	public required RPLOldTokenInfo RPLOldTokenInfo { get; init; }

	public static async Task<TokensContextRPLOld> ReadAsync(
		Func<string, Task<long?>> findDeploymentBlock,
		Storage storage, ContractsContext contractsContext, SyncOptions syncOptions,
		ILogger<TokensContextRPLOld> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading token snapshots");

		Task<BlobObject<TokensRPLOldSnapshot>?> readRPLOldTask =
			storage.ReadAsync<TokensRPLOldSnapshot>(Keys.TokensRPLOldSnapshot, cancellationToken);

		await contractsContext.IsFinished;

		ReadOnlyDictionary<string, RocketPoolContract> contracts = contractsContext.ContextContracts.AsReadOnly();
		string rplContractAddress = contracts["rocketTokenRPL"].Versions.Select(x => x.Address).Single();
		string rplOldContractAddress = contracts["rocketTokenRPLFixedSupply"].Versions.Select(x => x.Address).Single();

		BlobObject<TokensRPLOldSnapshot> rplOldSnapshot =
			await readRPLOldTask ??
			new BlobObject<TokensRPLOldSnapshot>
			{
				ProcessedBlockNumber = 0,
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

		return new TokensContextRPLOld
		{
			CurrentBlockHeight = rplOldSnapshot.ProcessedBlockNumber,

			RPLTokenAddress = rplContractAddress,
			RPLOldTokenAddress = rplOldContractAddress,
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
		};
	}

	public async Task SaveAsync(
		Storage storage, ILogger<TokensContextRPLOld> logger, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.TokensRPLOldSnapshot);
		await storage.WriteAsync(
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
	}
}