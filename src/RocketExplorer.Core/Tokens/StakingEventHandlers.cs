using System.Numerics;
using Nethereum.Contracts;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;

namespace RocketExplorer.Core.Tokens;

public class StakingEventHandlers
{
	public static void HandleRPLLegacyStaked(TokensSyncContext context, EventLog<RPLLegacyStakedEventDto> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.RPLTokenInfo.LegacyStakedDaily[key] =
			context.RPLTokenInfo.LegacyStakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.RPLTokenInfo.LegacyStakedTotal[key] = context.RPLTokenInfo.LegacyStakedTotal.GetLatestValueOrDefault() +
			eventLog.Event.Amount;
	}

	public static void HandleRPLLegacyUnstaked(TokensSyncContext context, EventLog<RPLLegacyWithdrawnEventDTO> eventLog)
	{
		BigInteger amount = eventLog.Event.Amount;

		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.RPLTokenInfo.LegacyUnstakedDaily[key] =
			context.RPLTokenInfo.LegacyUnstakedDaily.GetValueOrDefault(key) - eventLog.Event.Amount;
		context.RPLTokenInfo.LegacyStakedTotal[key] = context.RPLTokenInfo.LegacyStakedTotal.GetLatestValueOrDefault() -
			eventLog.Event.Amount;
	}

	public static void HandleRPLLegacyUnstaked(
		TokensSyncContext context, EventLog<RPLOrRPLLegacyWithdrawnEventDTO> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.RPLTokenInfo.LegacyUnstakedDaily[key] =
			context.RPLTokenInfo.LegacyUnstakedDaily.GetValueOrDefault(key) - eventLog.Event.Amount;
		context.RPLTokenInfo.LegacyStakedTotal[key] = context.RPLTokenInfo.LegacyStakedTotal.GetLatestValueOrDefault() -
			eventLog.Event.Amount;
	}

	public static void HandleRPLMegapoolStaked(TokensSyncContext context, EventLog<RPLStakedEventDTO> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.RPLTokenInfo.MegapoolStakedDaily[key] =
			context.RPLTokenInfo.MegapoolStakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.RPLTokenInfo.MegapoolStakedTotal[key] =
			context.RPLTokenInfo.MegapoolStakedTotal.GetLatestValueOrDefault() + eventLog.Event.Amount;
	}

	public static void HandleRPLMegapoolUnstaked(TokensSyncContext context, EventLog<RPLUnstakedEventDTO> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.RPLTokenInfo.MegapoolUnstakedDaily[key] =
			context.RPLTokenInfo.MegapoolUnstakedDaily.GetValueOrDefault(key) - eventLog.Event.Amount;
		context.RPLTokenInfo.MegapoolStakedTotal[key] =
			context.RPLTokenInfo.MegapoolStakedTotal.GetLatestValueOrDefault() - eventLog.Event.Amount;
	}
}