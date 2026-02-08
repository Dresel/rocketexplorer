using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokensSyncRPL(IOptions<SyncOptions> options, GlobalContext globalContext)
	: SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);

		TokensContextRPL context = await GlobalContext.TokensContextRPLFactory;
		context.ProcessingCompletionSource.TrySetResult();
	}

	protected override async Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default)
	{
		TokensContextRPL context = await GlobalContext.TokensContextRPLFactory;
		return context.CurrentBlockHeight;
	}

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		TokensContextRPL context = await GlobalContext.TokensContextRPLFactory;

		IEnumerable<IEventLog> rplEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RPLTokenAddress,], GlobalContext.Policy);

		foreach (IEventLog eventLog in rplEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, GlobalContext>(
				TokenEventHandlers.HandleRPLAsync, GlobalContext, cancellationToken);
		}
	}

	protected override async Task OnHandleBlocksErrorAsync(Exception e, CancellationToken cancellationToken)
	{
		await base.OnHandleBlocksErrorAsync(e, cancellationToken);

		TokensContextRPL context = await GlobalContext.TokensContextRPLFactory;
		context.ProcessingCompletionSource.TrySetException(e);
	}

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		TokensContextRPL context = await GlobalContext.TokensContextRPLFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}
}