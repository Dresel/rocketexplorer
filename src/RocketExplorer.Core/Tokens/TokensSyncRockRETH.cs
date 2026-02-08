using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Ethereum;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokensSyncRockRETH(IOptions<SyncOptions> options, GlobalContext globalContext)
	: SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);

		TokensContextRockRETH context = await GlobalContext.TokensContextRockRETHFactory;
		context.ProcessingCompletionSource.TrySetResult();
	}

	protected override async Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default)
	{
		TokensContextRockRETH context = await GlobalContext.TokensContextRockRETHFactory;
		return context.CurrentBlockHeight;
	}

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		TokensContextRockRETH context = await GlobalContext.TokensContextRockRETHFactory;

		if (context.RockRETHTokenAddress is not null)
		{
			IEnumerable<IEventLog> rockRETHEvents = await GlobalContext.Services.Web3.FilterAsync(
				fromBlock, toBlock, [typeof(TransferEventDTO),],
				[context.RockRETHTokenAddress,], GlobalContext.Policy);

			foreach (IEventLog eventLog in rockRETHEvents)
			{
				await eventLog.WhenIsAsync<TransferEventDTO, GlobalContext>(
					TokenEventHandlers.HandleRockRETHAsync, GlobalContext, cancellationToken);
			}
		}
	}

	protected override async Task OnHandleBlocksErrorAsync(Exception e, CancellationToken cancellationToken)
	{
		await base.OnHandleBlocksErrorAsync(e, cancellationToken);

		TokensContextRockRETH context = await GlobalContext.TokensContextRockRETHFactory;
		context.ProcessingCompletionSource.TrySetException(e);
	}

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		TokensContextRockRETH context = await GlobalContext.TokensContextRockRETHFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}
}