using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokensSync(IOptions<SyncOptions> options, GlobalContext globalContext) : SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);

		TokensContext context = await GlobalContext.TokensContextFactory;
		context.ProcessingCompletionSource.TrySetResult();
	}

	protected override async Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default)
	{
		TokensContext context = await GlobalContext.TokensContextFactory;
		return context.CurrentBlockHeight;
	}

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		TokensContext context = await GlobalContext.TokensContextFactory;

		IEnumerable<IEventLog> rplOldEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RPLOldTokenAddress,], GlobalContext.Policy);

		foreach (IEventLog eventLog in rplOldEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, GlobalContext>(
				TokenEventHandlers.HandleRPLOldAsync, GlobalContext, cancellationToken);
		}

		IEnumerable<IEventLog> rplEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO), typeof(RPLFixedSupplyBurnEventDTO),],
			[context.RPLTokenAddress,], GlobalContext.Policy);

		foreach (IEventLog eventLog in rplEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, GlobalContext>(
				TokenEventHandlers.HandleRPLAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<RPLFixedSupplyBurnEventDTO, GlobalContext>(
				TokenEventHandlers.Handle, GlobalContext, cancellationToken);
		}

		IEnumerable<IEventLog> rethEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RETHTokenAddress,], GlobalContext.Policy);

		foreach (IEventLog eventLog in rethEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, GlobalContext>(
				TokenEventHandlers.HandleRETHAsync, GlobalContext, cancellationToken);
		}

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

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		TokensContext context = await GlobalContext.TokensContextFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}
}