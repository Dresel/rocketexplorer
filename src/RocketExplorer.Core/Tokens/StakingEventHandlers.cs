using System.Threading;
using Nethereum.Contracts;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;

namespace RocketExplorer.Core.Tokens;

public class StakingEventHandlers
{
	public static async Task HandleRPLLegacyStaked(GlobalContext globalContext, EventLog<RPLLegacyStakedEventDto> eventLog, CancellationToken cancellationToken = default)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		TokensContext context = await globalContext.TokensContextFactory;

		context.StakedRPLInfo.LegacyStakedDaily[key] =
			context.StakedRPLInfo.LegacyStakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.StakedRPLInfo.LegacyStakedTotal[key] =
			context.StakedRPLInfo.LegacyStakedTotal.GetLatestValueOrDefault() +
			eventLog.Event.Amount;
	}

	public static async Task HandleRPLLegacyUnstaked(GlobalContext globalContext, EventLog<RPLLegacyWithdrawnEventDTO> eventLog, CancellationToken cancellationToken = default)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		TokensContext context = await globalContext.TokensContextFactory;

		context.StakedRPLInfo.LegacyUnstakedDaily[key] =
			context.StakedRPLInfo.LegacyUnstakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.StakedRPLInfo.LegacyStakedTotal[key] =
			context.StakedRPLInfo.LegacyStakedTotal.GetLatestValueOrDefault() -
			eventLog.Event.Amount;
	}

	public static async Task HandleRPLLegacyUnstaked(
		GlobalContext globalContext, EventLog<RPLOrRPLLegacyWithdrawnEventDTO> eventLog, CancellationToken cancellationToken = default)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		TokensContext context = await globalContext.TokensContextFactory;

		context.StakedRPLInfo.LegacyUnstakedDaily[key] =
			context.StakedRPLInfo.LegacyUnstakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.StakedRPLInfo.LegacyStakedTotal[key] =
			context.StakedRPLInfo.LegacyStakedTotal.GetLatestValueOrDefault() -
			eventLog.Event.Amount;
	}

	public static async Task HandleRPLMegapoolStaked(GlobalContext globalContext, EventLog<RPLStakedEventDTO> eventLog, CancellationToken cancellationToken = default)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		TokensContext context = await globalContext.TokensContextFactory;

		context.StakedRPLInfo.MegapoolStakedDaily[key] =
			context.StakedRPLInfo.MegapoolStakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.StakedRPLInfo.MegapoolStakedTotal[key] =
			context.StakedRPLInfo.MegapoolStakedTotal.GetLatestValueOrDefault() + eventLog.Event.Amount;
	}

	public static async Task HandleRPLMegapoolUnstaked(GlobalContext globalContext, EventLog<RPLUnstakedEventDTO> eventLog, CancellationToken cancellationToken = default)
	{
		DateOnly key = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)eventLog.Event.Time).DateTime);

		TokensContext context = await globalContext.TokensContextFactory;

		context.StakedRPLInfo.MegapoolUnstakedDaily[key] =
			context.StakedRPLInfo.MegapoolUnstakedDaily.GetValueOrDefault(key) + eventLog.Event.Amount;
		context.StakedRPLInfo.MegapoolStakedTotal[key] =
			context.StakedRPLInfo.MegapoolStakedTotal.GetLatestValueOrDefault() - eventLog.Event.Amount;
	}
}