using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using Polly.Retry;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core;

public abstract class SyncBase<TContext>(IOptions<SyncOptions> options, Storage storage, ILogger logger)
	where TContext : ContextBase
{
	private const long BlockRange = 100000;

	protected ILogger Logger { get; set; } = logger;

	protected SyncOptions Options { get; set; } = options.Value;

	protected AsyncRetryPolicy Policy { get; set; } = NethereumPolicies.Retry(logger);

	protected Storage Storage { get; set; } = storage;

	public async Task HandleBlocksAsync(
		Web3 web3, RocketStorageService rocketStorage, ReadOnlyDictionary<string, RocketPoolContract> contracts,
		long latestBlock, CancellationToken cancellationToken = default)
	{
		TContext context = await LoadContextAsync(web3, rocketStorage, contracts, cancellationToken);

		if (context.CurrentBlockHeight == latestBlock)
		{
			Logger.LogInformation("Up to date, nothing to do");
			return;
		}

		await BeforeHandleBlocksAsync(context, latestBlock, cancellationToken);

		long currentBlock = context.CurrentBlockHeight + 1;

		do
		{
			long toBlock = Math.Min(currentBlock + BlockRange, latestBlock);

			Logger.LogDebug("Processing block {FromBlock} to {ToBlock}", currentBlock, toBlock);
			await HandleBlocksAsync(context, currentBlock, toBlock, latestBlock, cancellationToken);
			context.CurrentBlockHeight = toBlock;

			currentBlock = toBlock + 1;
		}
		while (currentBlock <= latestBlock);

		await SaveContextAsync(context, cancellationToken);
	}

	protected virtual Task BeforeHandleBlocksAsync(TContext context, long latestBlock, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	protected abstract Task HandleBlocksAsync(
		TContext context, long fromBlock, long toBlock, long latestBlock, CancellationToken cancellationToken = default);

	protected abstract Task<TContext> LoadContextAsync(
		Web3 web3, RocketStorageService rocketStorage, ReadOnlyDictionary<string, RocketPoolContract> contracts,
		CancellationToken cancellationToken = default);

	protected abstract Task SaveContextAsync(TContext context, CancellationToken cancellationToken = default);
}