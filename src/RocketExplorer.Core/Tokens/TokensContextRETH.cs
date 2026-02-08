using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Core.Tokens;

public record class TokensContextRETH
{
	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required string RETHTokenAddress { get; init; }

	public required TokenInfo RETHTokenInfo { get; init; }

	public static async Task<TokensContextRETH> ReadAsync(
		Func<string, Task<long?>> findDeploymentBlock,
		Storage storage, ContractsContext contractsContext, SyncOptions syncOptions, ILogger<TokensContextRETH> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading {snapshot}", Keys.TokensRETHSnapshot);

		Task<BlobObject<TokensRETHSnapshot>?> readRETHTask =
			storage.ReadAsync<TokensRETHSnapshot>(Keys.TokensRETHSnapshot, cancellationToken);

		await contractsContext.IsFinished;

		ReadOnlyDictionary<string, RocketPoolContract> contracts = contractsContext.ContextContracts.AsReadOnly();
		string rethContractAddress = contracts["rocketTokenRETH"].Versions.Select(x => x.Address).Single();

		BlobObject<TokensRETHSnapshot> rethSnapshot =
			await readRETHTask ??
			new BlobObject<TokensRETHSnapshot>
			{
				ProcessedBlockNumber = await findDeploymentBlock(rethContractAddress) - 1 ??
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

		return new TokensContextRETH
		{
			CurrentBlockHeight = rethSnapshot.ProcessedBlockNumber,

			RETHTokenAddress = rethContractAddress,
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
		};
	}

	public async Task SaveAsync(
		Storage storage, ILogger<TokensContextRETH> logger, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.TokensRETHSnapshot);
		await storage.WriteAsync(
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
	}
}