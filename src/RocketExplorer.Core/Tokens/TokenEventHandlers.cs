using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Tokens;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokenEventHandlers
{
	public static async Task Handle(
		GlobalContext globalContext, EventLog<RPLFixedSupplyBurnEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		TokensContextRPLOld context = await globalContext.TokensContextRPLOldFactory;

		context.RPLOldTokenInfo.SwappedDaily[key] =
			context.RPLOldTokenInfo.SwappedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.RPLOldTokenInfo.SwappedTotal[key] =
			context.RPLOldTokenInfo.SwappedTotal.GetLatestValueOrDefault() + eventLog.Event.Amount;

		// RPLv1 tokens are irreversible stored in the RocketTokenRPL contract so we could interpret this as a supply reduction / burn
		context.RPLOldTokenInfo.SupplyTotal[key] =
			context.RPLOldTokenInfo.SupplyTotal.GetLatestValueOrDefault() - eventLog.Event.Amount;
		context.RPLOldTokenInfo.BurnsDaily[key] =
			context.RPLOldTokenInfo.SwappedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;

		globalContext.DashboardContext.RPLSwappedTotal = context.RPLOldTokenInfo.SwappedTotal.GetLatestValueOrDefault();
	}

	public static async Task HandleRETHAsync(
		GlobalContext globalContext, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		TokensContextRETH context = await globalContext.TokensContextRETHFactory;
		await HandleAsync(globalContext, context.RETHTokenInfo, TokenType.RETH, eventLog, cancellationToken);

		globalContext.DashboardContext.RETHSupplyTotal = context.RETHTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	public static async Task HandleRockRETHAsync(
		GlobalContext globalContext, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		TokensContextRockRETH context = await globalContext.TokensContextRockRETHFactory;
		await HandleAsync(globalContext, context.RockRETHTokenInfo, TokenType.RockRETH, eventLog, cancellationToken);

		globalContext.DashboardContext.RockRETHSupplyTotal =
			context.RockRETHTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	public static async Task HandleRPLAsync(
		GlobalContext globalContext, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		TokensContextRPL context = await globalContext.TokensContextRPLFactory;
		await HandleAsync(globalContext, context.RPLTokenInfo, TokenType.RPL, eventLog, cancellationToken);

		globalContext.DashboardContext.RPLSupplyTotal = context.RPLTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	public static async Task HandleRPLOldAsync(
		GlobalContext globalContext, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		TokensContextRPLOld context = await globalContext.TokensContextRPLOldFactory;
		await HandleAsync(globalContext, context.RPLOldTokenInfo, TokenType.RPLOld, eventLog, cancellationToken);

		globalContext.DashboardContext.RPLOldSupplyTotal =
			context.RPLOldTokenInfo.SupplyTotal.GetLatestValueOrDefault();
	}

	private static async Task HandleAsync(
		GlobalContext globalContext,
		TokenInfo tokenInfo, TokenType tokenType, EventLog<TransferEventDTO> eventLog,
		CancellationToken cancellationToken = default)
	{
		if (eventLog.Event.Value.IsZero || string.Equals(
				eventLog.Event.From, eventLog.Event.To, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		string fromAddress = eventLog.Event.From;

		if (!fromAddress.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
		{
			BigInteger fromBalance =
				(tokenInfo.Holders.GetValueOrDefault(fromAddress)?.Balance ?? 0) - eventLog.Event.Value;

			if (fromBalance.IsZero)
			{
				tokenInfo.Holders.Remove(fromAddress);

				_ = globalContext.Services.GlobalIndexService.UpdateEntryAsync(
					eventLog.Event.From.RemoveHexPrefix(), eventLog.Event.From.HexToByteArray(),
					new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
					entry =>
					{
						IndexEntryType indexEntryType = tokenType switch
						{
							TokenType.RPL => IndexEntryType.RPLHolder,
							TokenType.RPLOld => IndexEntryType.RPLOldHolder,
							TokenType.RETH => IndexEntryType.RETHHolder,
							TokenType.RockRETH => IndexEntryType.RockRETHHolder,
							_ => throw new InvalidOperationException("Unknown token type"),
						};

						entry.Type &= ~indexEntryType;
					}, entry => entry.Type == 0, cancellationToken);

				await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
					fromAddress.HexToByteArray(), cancellationToken: cancellationToken);
			}
			else
			{
				if (!tokenInfo.Holders.ContainsKey(fromAddress))
				{
					globalContext.GetLogger<TokenEventHandlers>().LogError(
						"Holder Address is missing: {Address} ({Block} / {Transaction})",
						fromAddress, eventLog.Log.BlockNumber, eventLog.Log.TransactionHash);
				}

				tokenInfo.Holders[fromAddress] = tokenInfo.Holders[fromAddress] with
				{
					Balance = fromBalance,
				};
			}
		}
		else
		{
			BlockWithTransactions block = await globalContext.Policy.ExecuteAsync(() =>
				globalContext.Services.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
					eventLog.Log.BlockNumber));
			DateOnly key =
				DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).DateTime);

			tokenInfo.MintsDaily[key] = tokenInfo.MintsDaily.GetValueOrDefault(key) + eventLog.Event.Value;
			tokenInfo.SupplyTotal[key] = tokenInfo.SupplyTotal.GetLatestValueOrDefault() + eventLog.Event.Value;
		}

		string toAddress = eventLog.Event.To;

		if (!toAddress.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
		{
			if (!tokenInfo.Holders.ContainsKey(toAddress))
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					eventLog.Event.To.RemoveHexPrefix(), eventLog.Event.To.HexToByteArray(),
					new EventIndex(eventLog.Log.BlockNumber, eventLog.Log.LogIndex),
					entry =>
					{
						entry.Type |= tokenType switch
						{
							TokenType.RPL => IndexEntryType.RPLHolder,
							TokenType.RPLOld => IndexEntryType.RPLOldHolder,
							TokenType.RETH => IndexEntryType.RETHHolder,
							TokenType.RockRETH => IndexEntryType.RockRETHHolder,
							_ => throw new InvalidOperationException("Unknown token type"),
						};
						entry.Address = eventLog.Event.To.HexToByteArray();
					}, cancellationToken: cancellationToken);

				await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
					toAddress.HexToByteArray(), cancellationToken: cancellationToken);

				tokenInfo.Holders[toAddress] = new HolderEntry
				{
					Address = toAddress,
					Balance = 0,
				};
			}

			BigInteger toBalance =
				(tokenInfo.Holders.GetValueOrDefault(toAddress)?.Balance ?? 0) + eventLog.Event.Value;

			tokenInfo.Holders[toAddress] = tokenInfo.Holders[toAddress] with
			{
				Balance = toBalance,
			};
		}
		else
		{
			BlockWithTransactions block = await globalContext.Services.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber
				.SendRequestAsync(eventLog.Log.BlockNumber);
			DateOnly key =
				DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).DateTime);

			tokenInfo.BurnsDaily[key] = tokenInfo.BurnsDaily.GetValueOrDefault(key) + eventLog.Event.Value;
			tokenInfo.SupplyTotal[key] = tokenInfo.SupplyTotal.GetLatestValueOrDefault() - eventLog.Event.Value;
		}
	}
}