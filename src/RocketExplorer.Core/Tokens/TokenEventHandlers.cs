using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokenEventHandlers
{
	public static Task Handle(
		TokensSyncContext context, EventLog<RPLFixedSupplyBurnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);
		context.RPLTokenInfo.SwappedDaily[key] = context.RPLTokenInfo.SwappedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.RPLTokenInfo.SwappedTotal[key] = context.RPLTokenInfo.SwappedTotal.GetLatestValueOrDefault() + eventLog.Event.Amount;

		context.DashboardInfo.RPLSwappedTotal = context.RPLTokenInfo.SwappedTotal.GetLatestValueOrDefault();

		return Task.CompletedTask;
	}

	public static async Task HandleRETHAsync(
		TokensSyncContext context, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		await HandleAsync(context, context.RETHTokenInfo, eventLog, cancellationToken);

		context.DashboardInfo.RETHSupplyTotal = context.RETHTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	public static async Task HandleRPLAsync(
		TokensSyncContext context, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		await HandleAsync(context, context.RPLTokenInfo, eventLog, cancellationToken);

		context.DashboardInfo.RPLSupplyTotal = context.RPLTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	public static async Task HandleRPLOldAsync(
		TokensSyncContext context, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		await HandleAsync(context, context.RPLOldTokenInfo, eventLog, cancellationToken);

		context.DashboardInfo.RPLOldSupplyTotal = context.RPLOldTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	private static async Task HandleAsync(
		TokensSyncContext context,
		TokenInfo tokenInfo, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		if (!eventLog.Event.From.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
		{
			tokenInfo.Holders[eventLog.Event.From] =
				tokenInfo.Holders.GetValueOrDefault(eventLog.Event.From) - eventLog.Event.Value;
		}
		else
		{
			BlockWithTransactions block = await context.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber
				.SendRequestAsync(eventLog.Log.BlockNumber);
			DateOnly key =
				DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).DateTime);

			tokenInfo.MintsPerDay[key] = tokenInfo.MintsPerDay.GetValueOrDefault(key) + eventLog.Event.Value;
			tokenInfo.SupplyTotal[key] = tokenInfo.SupplyTotal.GetLatestValueOrDefault() + eventLog.Event.Value;
		}

		if (!eventLog.Event.To.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
		{
			tokenInfo.Holders[eventLog.Event.To] =
				tokenInfo.Holders.GetValueOrDefault(eventLog.Event.To) + eventLog.Event.Value;
		}
		else
		{
			BlockWithTransactions block = await context.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber
				.SendRequestAsync(eventLog.Log.BlockNumber);
			DateOnly key =
				DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).DateTime);

			tokenInfo.BurnsPerDay[key] = tokenInfo.BurnsPerDay.GetValueOrDefault(key) + eventLog.Event.Value;
			tokenInfo.SupplyTotal[key] = tokenInfo.SupplyTotal.GetLatestValueOrDefault() - eventLog.Event.Value;
		}
	}
}