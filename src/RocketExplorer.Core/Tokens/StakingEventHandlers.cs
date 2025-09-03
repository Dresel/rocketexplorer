using System.Numerics;
using Nethereum.Contracts;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;

namespace RocketExplorer.Core.Tokens;

public class StakingEventHandlers
{
	public static void HandleRPLLegacyStaked(TokensSyncContext context, EventLog<RPLLegacyStakedEventDto> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.StakedRPLInfo.LegacyStakedDaily[key] =
			context.StakedRPLInfo.LegacyStakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.StakedRPLInfo.LegacyStakedTotal[key] = context.StakedRPLInfo.LegacyStakedTotal.GetLatestValueOrDefault() +
			eventLog.Event.Amount;
	}

	public static void HandleRPLLegacyUnstaked(TokensSyncContext context, EventLog<RPLLegacyWithdrawnEventDTO> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.StakedRPLInfo.LegacyUnstakedDaily[key] =
			context.StakedRPLInfo.LegacyUnstakedDaily.GetValueOrDefault(key) - eventLog.Event.Amount;
		context.StakedRPLInfo.LegacyStakedTotal[key] = context.StakedRPLInfo.LegacyStakedTotal.GetLatestValueOrDefault() -
			eventLog.Event.Amount;
	}

	public static void HandleRPLLegacyUnstaked(
		TokensSyncContext context, EventLog<RPLOrRPLLegacyWithdrawnEventDTO> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.StakedRPLInfo.LegacyUnstakedDaily[key] =
			context.StakedRPLInfo.LegacyUnstakedDaily.GetValueOrDefault(key) - eventLog.Event.Amount;
		context.StakedRPLInfo.LegacyStakedTotal[key] = context.StakedRPLInfo.LegacyStakedTotal.GetLatestValueOrDefault() -
			eventLog.Event.Amount;
	}

	public static void HandleRPLMegapoolStaked(TokensSyncContext context, EventLog<RPLStakedEventDTO> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.StakedRPLInfo.MegapoolStakedDaily[key] =
			context.StakedRPLInfo.MegapoolStakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.StakedRPLInfo.MegapoolStakedTotal[key] =
			context.StakedRPLInfo.MegapoolStakedTotal.GetLatestValueOrDefault() + eventLog.Event.Amount;
	}

	public static void HandleRPLMegapoolUnstaked(TokensSyncContext context, EventLog<RPLUnstakedEventDTO> eventLog)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		context.StakedRPLInfo.MegapoolUnstakedDaily[key] =
			context.StakedRPLInfo.MegapoolUnstakedDaily.GetValueOrDefault(key) - eventLog.Event.Amount;
		context.StakedRPLInfo.MegapoolStakedTotal[key] =
			context.StakedRPLInfo.MegapoolStakedTotal.GetLatestValueOrDefault() - eventLog.Event.Amount;
	}
}