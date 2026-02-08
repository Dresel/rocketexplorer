using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokensSyncRPLOld(IOptions<SyncOptions> options, GlobalContext globalContext)
	: SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);

		TokensContextRPLOld context = await GlobalContext.TokensContextRPLOldFactory;
		context.ProcessingCompletionSource.TrySetResult();
	}

	protected override async Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default)
	{
		TokensContextRPLOld context = await GlobalContext.TokensContextRPLOldFactory;
		return context.CurrentBlockHeight;
	}

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		TokensContextRPLOld context = await GlobalContext.TokensContextRPLOldFactory;

		IEnumerable<IEventLog> rplOldEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RPLOldTokenAddress,], GlobalContext.Policy);

		foreach (IEventLog eventLog in rplOldEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, GlobalContext>(
				TokenEventHandlers.HandleRPLOldAsync, GlobalContext, cancellationToken);
		}

		IEnumerable<IEventLog> rplEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(RPLFixedSupplyBurnEventDTO),],
			[context.RPLTokenAddress,], GlobalContext.Policy);

		foreach (IEventLog eventLog in rplEvents)
		{
			await eventLog.WhenIsAsync<RPLFixedSupplyBurnEventDTO, GlobalContext>(
				TokenEventHandlers.Handle, GlobalContext, cancellationToken);
		}
	}

	protected override async Task OnHandleBlocksErrorAsync(Exception e, CancellationToken cancellationToken)
	{
		await base.OnHandleBlocksErrorAsync(e, cancellationToken);

		TokensContextRPLOld context = await GlobalContext.TokensContextRPLOldFactory;
		context.ProcessingCompletionSource.TrySetException(e);
	}

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		TokensContextRPLOld context = await GlobalContext.TokensContextRPLOldFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}
}