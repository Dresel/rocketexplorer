using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RocketExplorer.Core;

public abstract class SyncBase<TContext>(IOptions<SyncOptions> options)
	where TContext : ContextBase
{
	private const long BlockRange = 25_000;

	protected SyncOptions Options { get; set; } = options.Value;

	public async Task HandleBlocksAsync(
		ContextBase contextBase,
		CancellationToken cancellationToken = default)
	{
		TContext context = await LoadContextAsync(contextBase, cancellationToken);

		if (context.CurrentBlockHeight == context.LatestBlockHeight)
		{
			context.Logger.LogInformation("Up to date, nothing to do");
			return;
		}

		await BeforeHandleBlocksAsync(context, cancellationToken);

		long startBlock = context.CurrentBlockHeight + 1;
		long totalBlocks = context.LatestBlockHeight - startBlock + 2;

		long currentBlock = startBlock;

		Stopwatch stopwatch = Stopwatch.StartNew();

		do
		{
			long toBlock = Math.Min(currentBlock + BlockRange - 1, context.LatestBlockHeight);
			long processedBlocks = toBlock - startBlock + 1;

			double remainingTimeInMilliseconds = (double)stopwatch.ElapsedMilliseconds / processedBlocks *
				(totalBlocks - processedBlocks);

			context.Logger.LogInformation(
				"Processing block {FromBlock} to {ToBlock}, estimated remaining time: {RemainingTime}", currentBlock,
				toBlock,
				double.IsNormal(remainingTimeInMilliseconds)
					? TimeSpan.FromMilliseconds(remainingTimeInMilliseconds)
					: "-");

			await HandleBlocksAsync(context, currentBlock, toBlock, cancellationToken);
			context.CurrentBlockHeight = toBlock;

			currentBlock = toBlock + 1;
		}
		while (currentBlock <= context.LatestBlockHeight);

		await SaveContextAsync(context, cancellationToken);
	}

	protected virtual Task BeforeHandleBlocksAsync(TContext context, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	protected abstract Task HandleBlocksAsync(
		TContext context, long fromBlock, long toBlock,
		CancellationToken cancellationToken = default);

	protected abstract Task<TContext> LoadContextAsync(
		ContextBase contextBase,
		CancellationToken cancellationToken = default);

	protected abstract Task SaveContextAsync(TContext context, CancellationToken cancellationToken = default);
}