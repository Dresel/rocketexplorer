using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using RocketExplorer.Shared;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokenEventHandlers
{
	public static Task Handle(
		TokensSyncContext context, EventLog<RPLFixedSupplyBurnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);
		context.RPLOldTokenInfo.SwappedDaily[key] =
			context.RPLOldTokenInfo.SwappedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.RPLOldTokenInfo.SwappedTotal[key] =
			context.RPLOldTokenInfo.SwappedTotal.GetLatestValueOrDefault() + eventLog.Event.Amount;

		// RPLv1 tokens are irreversible stored in the RocketTokenRPL contract so we could interpret this as a supply reduction / burn
		context.RPLOldTokenInfo.SupplyTotal[key] =
			context.RPLOldTokenInfo.SupplyTotal.GetLatestValueOrDefault() - eventLog.Event.Amount;
		context.RPLOldTokenInfo.BurnsDaily[key] =
			context.RPLOldTokenInfo.SwappedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;

		context.DashboardInfo.RPLSwappedTotal = context.RPLOldTokenInfo.SwappedTotal.GetLatestValueOrDefault();

		return Task.CompletedTask;
	}

	public static async Task HandleRETHAsync(
		TokensSyncContext context, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		await HandleAsync(context, context.RETHTokenInfo, TokenType.RETH, eventLog, cancellationToken);

		context.DashboardInfo.RETHSupplyTotal = context.RETHTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	public static async Task HandleRPLAsync(
		TokensSyncContext context, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		await HandleAsync(context, context.RPLTokenInfo, TokenType.RPL, eventLog, cancellationToken);

		context.DashboardInfo.RPLSupplyTotal = context.RPLTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	public static async Task HandleRPLOldAsync(
		TokensSyncContext context, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		await HandleAsync(context, context.RPLOldTokenInfo, TokenType.RPLOld, eventLog, cancellationToken);

		context.DashboardInfo.RPLOldSupplyTotal = context.RPLOldTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	private static async Task HandleAsync(
		TokensSyncContext context,
		TokenInfo tokenInfo, TokenType tokenType, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		if (!eventLog.Event.From.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
		{
			BigInteger balance = tokenInfo.Holders.GetValueOrDefault(eventLog.Event.From) - eventLog.Event.Value;

			if (balance.IsZero)
			{
				tokenInfo.Holders.Remove(eventLog.Event.From);

				await context.GlobalIndexService.UpdateOrRemoveEntryAsync(
					eventLog.Event.From.HexToByteArray(), eventLog.Event.From.RemoveHexPrefix(),
					entry =>
					{
						IndexEntryType indexEntryType = tokenType switch
						{
							TokenType.RPL => IndexEntryType.RPLHolder,
							TokenType.RPLOld => IndexEntryType.RPLOldHolder,
							TokenType.RETH => IndexEntryType.RETHHolder,
							_ => throw new InvalidOperationException("Unknown token type"),
						};

						entry.Type &= ~indexEntryType;
					}, cancellationToken);
			}
			else
			{
				tokenInfo.Holders[eventLog.Event.From] = balance;
			}
		}
		else
		{
			BlockWithTransactions block = await context.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber
				.SendRequestAsync(eventLog.Log.BlockNumber);
			DateOnly key =
				DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).DateTime);

			tokenInfo.MintsDaily[key] = tokenInfo.MintsDaily.GetValueOrDefault(key) + eventLog.Event.Value;
			tokenInfo.SupplyTotal[key] = tokenInfo.SupplyTotal.GetLatestValueOrDefault() + eventLog.Event.Value;
		}

		if (!eventLog.Event.To.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
		{
			if (!tokenInfo.Holders.ContainsKey(eventLog.Event.To))
			{
				await context.GlobalIndexService.AddOrUpdateEntryAsync(
					eventLog.Event.To.HexToByteArray(), eventLog.Event.To.RemoveHexPrefix(),
					entry =>
					{
						entry.Type |= tokenType switch
						{
							TokenType.RPL => IndexEntryType.RPLHolder,
							TokenType.RPLOld => IndexEntryType.RPLOldHolder,
							TokenType.RETH => IndexEntryType.RETHHolder,
							_ => throw new InvalidOperationException("Unknown token type"),
						};
					}, cancellationToken);
			}

			tokenInfo.Holders[eventLog.Event.To] =
				tokenInfo.Holders.GetValueOrDefault(eventLog.Event.To) + eventLog.Event.Value;
		}
		else
		{
			BlockWithTransactions block = await context.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber
				.SendRequestAsync(eventLog.Log.BlockNumber);
			DateOnly key =
				DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).DateTime);

			tokenInfo.BurnsDaily[key] = tokenInfo.BurnsDaily.GetValueOrDefault(key) + eventLog.Event.Value;
			tokenInfo.SupplyTotal[key] = tokenInfo.SupplyTotal.GetLatestValueOrDefault() - eventLog.Event.Value;
		}
	}
}