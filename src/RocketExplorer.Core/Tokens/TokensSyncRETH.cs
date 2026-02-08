using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Ethereum;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokensSyncRETH(IOptions<SyncOptions> options, GlobalContext globalContext)
	: SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);

		TokensContextRETH context = await GlobalContext.TokensContextRETHFactory;
		context.ProcessingCompletionSource.TrySetResult();
	}

	protected override async Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default)
	{
		TokensContextRETH context = await GlobalContext.TokensContextRETHFactory;
		return context.CurrentBlockHeight;
	}

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		TokensContextRETH context = await GlobalContext.TokensContextRETHFactory;

		IEnumerable<IEventLog> rethEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RETHTokenAddress,], GlobalContext.Policy);

		foreach (IEventLog eventLog in rethEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, GlobalContext>(
				TokenEventHandlers.HandleRETHAsync, GlobalContext, cancellationToken);
		}
	}

	protected override async Task OnHandleBlocksErrorAsync(Exception e, CancellationToken cancellationToken)
	{
		await base.OnHandleBlocksErrorAsync(e, cancellationToken);

		TokensContextRETH context = await GlobalContext.TokensContextRETHFactory;
		context.ProcessingCompletionSource.TrySetException(e);
	}

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		TokensContextRETH context = await GlobalContext.TokensContextRETHFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}
}