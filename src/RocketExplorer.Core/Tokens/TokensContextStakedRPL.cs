using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Nethereum.Util;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Core.Tokens;

public record class TokensContextStakedRPL
{
	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public required string[] PostSaturn1RocketNodeStakingAddresses { get; set; }

	public required string[] PreSaturn1RocketNodeStakingAddresses { get; set; }

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required StakedRPLInfo StakedRPLInfo { get; set; }

	public static async Task<TokensContextStakedRPL> ReadAsync(
		Func<string, Task<long?>> findDeploymentBlock,
		Storage storage, ContractsContext contractsContext, SyncOptions syncOptions,
		ILogger<TokensContextStakedRPL> logger,
		CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Loading {snapshot}", Keys.TokensStakedRPLSnapshot);

		Task<BlobObject<StakedRPLSnapshot>?> readStakedRPLTask =
			storage.ReadAsync<StakedRPLSnapshot>(Keys.TokensStakedRPLSnapshot, cancellationToken);

		await contractsContext.IsFinished;

		ReadOnlyDictionary<string, RocketPoolContract> contracts = contractsContext.ContextContracts.AsReadOnly();
		string rplContractAddress = contracts["rocketTokenRPL"].Versions.Select(x => x.Address).Single();

		BlobObject<StakedRPLSnapshot> stakedRPLSnapshot =
			await readStakedRPLTask ??
			new BlobObject<StakedRPLSnapshot>
			{
				ProcessedBlockNumber = await findDeploymentBlock(rplContractAddress) - 1 ??
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

		return new TokensContextStakedRPL
		{
			CurrentBlockHeight = stakedRPLSnapshot.ProcessedBlockNumber,

			PreSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"].Versions
				.Where(x => x.Version <= 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS,]).ToArray(),
			PostSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"].Versions
				.Where(x => x.Version > 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS,]).ToArray(),

			StakedRPLInfo = new StakedRPLInfo
			{
				LegacyStakedTotal = stakedRPLSnapshot.Data.LegacyStakedTotal,
				LegacyStakedDaily = stakedRPLSnapshot.Data.LegacyStakedDaily,
				LegacyUnstakedDaily = stakedRPLSnapshot.Data.LegacyUnstakedDaily,
				MegapoolStakedTotal = stakedRPLSnapshot.Data.MegapoolStakedTotal,
				MegapoolStakedDaily = stakedRPLSnapshot.Data.MegapoolStakedDaily,
				MegapoolUnstakedDaily = stakedRPLSnapshot.Data.MegapoolUnstakedDaily,
			},
		};
	}

	public async Task SaveAsync(
		Storage storage, ILogger<TokensContextStakedRPL> logger, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.TokensStakedRPLSnapshot);
		await storage.WriteAsync(
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
	}
}