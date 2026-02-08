using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;

namespace RocketExplorer.Core.Tokens;

public class TokensSyncStakedRPL(IOptions<SyncOptions> options, GlobalContext globalContext)
	: SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);

		TokensContextStakedRPL context = await GlobalContext.TokensContextStakedRPLFactory;
		context.ProcessingCompletionSource.TrySetResult();
	}

	protected override async Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default)
	{
		TokensContextStakedRPL context = await GlobalContext.TokensContextStakedRPLFactory;
		return context.CurrentBlockHeight;
	}

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		TokensContextStakedRPL context = await GlobalContext.TokensContextStakedRPLFactory;

		IEnumerable<IEventLog> preSaturn1StakingEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [
				typeof(RPLLegacyStakedEventDto),
				typeof(RPLOrRPLLegacyWithdrawnEventDTO),
			],
			context.PreSaturn1RocketNodeStakingAddresses, GlobalContext.Policy);

		foreach (IEventLog eventLog in preSaturn1StakingEvents)
		{
			await eventLog.WhenIsAsync<RPLLegacyStakedEventDto, GlobalContext>(
				StakingEventHandlers.HandleRPLLegacyStaked, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<RPLOrRPLLegacyWithdrawnEventDTO, GlobalContext>(
				StakingEventHandlers.HandleRPLLegacyUnstaked, GlobalContext, cancellationToken);
		}

		IEnumerable<IEventLog> postSaturn1StakingEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [
				typeof(RPLLegacyWithdrawnEventDTO),
				typeof(RPLStakedEventDTO),
				typeof(RPLUnstakedEventDTO),
			],
			context.PostSaturn1RocketNodeStakingAddresses, GlobalContext.Policy);

		foreach (IEventLog eventLog in postSaturn1StakingEvents)
		{
			await eventLog.WhenIsAsync<RPLLegacyWithdrawnEventDTO, GlobalContext>(
				StakingEventHandlers.HandleRPLLegacyUnstaked, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<RPLStakedEventDTO, GlobalContext>(
				StakingEventHandlers.HandleRPLMegapoolStaked, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<RPLUnstakedEventDTO, GlobalContext>(
				StakingEventHandlers.HandleRPLMegapoolUnstaked, GlobalContext, cancellationToken);
		}
	}

	protected override async Task OnHandleBlocksErrorAsync(Exception e, CancellationToken cancellationToken)
	{
		await base.OnHandleBlocksErrorAsync(e, cancellationToken);

		TokensContextStakedRPL context = await GlobalContext.TokensContextStakedRPLFactory;
		context.ProcessingCompletionSource.TrySetException(e);
	}

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		TokensContextStakedRPL context = await GlobalContext.TokensContextStakedRPLFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}
}