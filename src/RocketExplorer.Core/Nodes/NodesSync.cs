using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes.EventHandlers;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketMegapoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolDelegate.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketMinipoolQueue.ContractDefinition;
using RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;

namespace RocketExplorer.Core.Nodes;

public class NodesSync(IOptions<SyncOptions> options, GlobalContext globalContext)
	: SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);

		NodesContext context = await GlobalContext.NodesContextFactory;
		context.ProcessingCompletionSource.TrySetResult();
	}

	protected override async Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default)
	{
		NodesContext context = await GlobalContext.NodesContextFactory;
		return context.CurrentBlockHeight;
	}

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		NodesContext context = await GlobalContext.NodesContextFactory;

		IEnumerable<IEventLog> nodeAddedEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(NodeRegisteredEventDTO),],
			context.RocketNodeManagerAddresses, GlobalContext.Policy);

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			await eventLog.WhenIsAsync<NodeRegisteredEventDTO, GlobalContext>(
				NodeEventsEventHandler.HandleAsync, GlobalContext, cancellationToken);
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
				StakingEventHandlers.HandleRPLLegacyStakedAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<RPLOrRPLLegacyWithdrawnEventDTO, GlobalContext>(
				StakingEventHandlers.HandleRPLLegacyUnstakedAsync, GlobalContext, cancellationToken);
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
				StakingEventHandlers.HandleRPLLegacyUnstakedAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<RPLStakedEventDTO, GlobalContext>(
				StakingEventHandlers.HandleRPLMegapoolStakedAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<RPLUnstakedEventDTO, GlobalContext>(
				StakingEventHandlers.HandleRPLMegapoolUnstakedAsync, GlobalContext, cancellationToken);
		}

		IEnumerable<IEventLog> minipoolCreatedEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(MinipoolCreatedEventDTO),],
			context.ValidatorInfo.RocketMinipoolManagerAddresses, GlobalContext.Policy);

		foreach (IEventLog eventLog in minipoolCreatedEvents)
		{
			await eventLog.WhenIsAsync<MinipoolCreatedEventDTO, GlobalContext>(
				MinipoolCreatedEventHandler.HandleAsync, GlobalContext, cancellationToken);
		}

		List<IEventLog> validatorEvents = (await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock,
			[
				typeof(MegapoolValidatorEnqueuedEventDTO),
				typeof(MegapoolValidatorDequeuedEventDTO),
				typeof(MegapoolValidatorAssignedEventDTO),
				typeof(MegapoolValidatorDissolvedEventDTO),
				typeof(MegapoolValidatorStakedEventDTO),
				typeof(MegapoolValidatorExitingEventDTO),
				typeof(MegapoolValidatorExitedEventDTO),
				typeof(MinipoolPrestakedEventDTO),
				typeof(MinipoolEnqueuedEventDTO),
				typeof(MinipoolDequeuedEventDTO),
				typeof(StatusUpdatedEventDTO),
				////typeof(MinipoolVacancyPreparedEventDTO),
				////typeof(MinipoolPromotedEventDTO),
				////typeof(MinipoolDestroyedEventDTO),
				typeof(EtherWithdrawalProcessedEventDTO), // Exit
			], [], GlobalContext.Policy)).ToList();

		foreach (IEventLog eventLog in validatorEvents)
		{
			await eventLog.WhenIsAsync<MinipoolPrestakedEventDTO, GlobalContext>(
				MinipoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MinipoolEnqueuedEventDTO, GlobalContext>(
				MinipoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MinipoolDequeuedEventDTO, GlobalContext>(
				MinipoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<EtherWithdrawalProcessedEventDTO, GlobalContext>(
				MinipoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<StatusUpdatedEventDTO, GlobalContext>(
				MinipoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorEnqueuedEventDTO, GlobalContext>(
				MegapoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorDequeuedEventDTO, GlobalContext>(
				MegapoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorAssignedEventDTO, GlobalContext>(
				MegapoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorDissolvedEventDTO, GlobalContext>(
				MegapoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorStakedEventDTO, GlobalContext>(
				MegapoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorExitingEventDTO, GlobalContext>(
				MegapoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<MegapoolValidatorExitedEventDTO, GlobalContext>(
				MegapoolEventHandlers.HandleAsync, GlobalContext, cancellationToken);
		}
	}

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		NodesContext context = await GlobalContext.NodesContextFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}
}