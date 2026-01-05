using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RocketExplorer.Core.Contracts;

public abstract class SyncBase(IOptions<SyncOptions> syncOptions, GlobalContext globalContext)
{
	protected const long BlockRange = 10_000;

	public SyncOptions Options { get; } = syncOptions.Value;

	protected GlobalContext GlobalContext { get; } = globalContext;

	public async Task HandleBlocksAsync(CancellationToken cancellationToken = default)
	{
		long currentBlockHeight = await GetCurrentBlockHeightAsync(cancellationToken);

		if (currentBlockHeight >= GlobalContext.LatestBlockHeight)
		{
			GlobalContext.GetLogger<SyncBase>().LogInformation("Up to date, nothing to do");
			await AfterHandleBlocksAsync(false, cancellationToken);
			return;
		}

		await BeforeHandleBlocksAsync(cancellationToken);

		// Refetch in case it changed in BeforeHandleBlocksAsync
		currentBlockHeight = await GetCurrentBlockHeightAsync(cancellationToken);

		long startBlock = currentBlockHeight + 1;
		long totalBlocks = GlobalContext.LatestBlockHeight - startBlock + 2;

		long currentBlock = startBlock;

		Stopwatch stopwatch = Stopwatch.StartNew();

		do
		{
			long toBlock = Math.Min(currentBlock + BlockRange - 1, GlobalContext.LatestBlockHeight);
			long processedBlocks = toBlock - startBlock + 1;

			double remainingTimeInMilliseconds = (double)stopwatch.ElapsedMilliseconds / processedBlocks *
				(totalBlocks - processedBlocks);

			bool isNormal = double.IsNormal(remainingTimeInMilliseconds);

			GlobalContext.GetLogger<SyncBase>().LogInformation(
				"Processing block {FromBlock} to {ToBlock}, estimated remaining time: {RemainingTime}", currentBlock,
				toBlock,
				isNormal ? TimeSpan.FromMilliseconds(remainingTimeInMilliseconds) : "-");

			try
			{
				await HandleBlocksAsync(currentBlock, toBlock, cancellationToken);
			}
			catch (Exception e)
			{
				GlobalContext.GetLogger<SyncBase>().LogError(
					e, "Error processing blocks {FromBlock} to {ToBlock}", currentBlock, toBlock);

				await OnHandleBlocksErrorAsync(e, cancellationToken);
				throw;
			}

			await SetCurrentBlockHeightAsync(toBlock, cancellationToken);

			currentBlock = toBlock + 1;
		}
		while (currentBlock <= GlobalContext.LatestBlockHeight);

		await AfterHandleBlocksAsync(true, cancellationToken);

		GlobalContext.GetLogger<SyncBase>().LogInformation("{Type}: Block processing finished", GetType().Name);
	}

	protected virtual Task OnHandleBlocksErrorAsync(Exception e, CancellationToken cancellationToken) => Task.CompletedTask;

	protected virtual Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken) => Task.CompletedTask;

	protected virtual Task BeforeHandleBlocksAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	protected abstract Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default);

	protected abstract Task HandleBlocksAsync(
		long fromBlock, long toBlock, CancellationToken cancellationToken = default);

	protected abstract Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default);
}