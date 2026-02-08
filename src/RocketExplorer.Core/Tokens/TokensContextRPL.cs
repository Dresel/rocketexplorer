using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Core.Tokens;

public record class TokensContextRPL
{
	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required string RPLTokenAddress { get; init; }

	public required TokenInfo RPLTokenInfo { get; init; }

	public static async Task<TokensContextRPL> ReadAsync(
		Func<string, Task<long?>> findDeploymentBlock,
		Storage storage, ContractsContext contractsContext, SyncOptions syncOptions, ILogger<TokensContextRPL> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading {snapshot}", Keys.TokensRPLSnapshot);

		Task<BlobObject<TokensRPLSnapshot>?> readRPLTask =
			storage.ReadAsync<TokensRPLSnapshot>(Keys.TokensRPLSnapshot, cancellationToken);

		await contractsContext.IsFinished;

		ReadOnlyDictionary<string, RocketPoolContract> contracts = contractsContext.ContextContracts.AsReadOnly();
		string rplContractAddress = contracts["rocketTokenRPL"].Versions.Select(x => x.Address).Single();

		BlobObject<TokensRPLSnapshot> rplSnapshot =
			await readRPLTask ??
			new BlobObject<TokensRPLSnapshot>
			{
				ProcessedBlockNumber = await findDeploymentBlock(rplContractAddress) - 1 ??
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

		return new TokensContextRPL
		{
			CurrentBlockHeight = rplSnapshot.ProcessedBlockNumber,

			RPLTokenAddress = rplContractAddress,
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
		};
	}

	public async Task SaveAsync(
		Storage storage, ILogger<TokensContextRPL> logger, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.TokensRPLSnapshot);
		await storage.WriteAsync(
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
	}
}