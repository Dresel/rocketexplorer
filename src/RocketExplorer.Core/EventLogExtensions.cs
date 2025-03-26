using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace RocketExplorer.Core;

public static class EventLogExtensions
{
	public static void WhenIs<TEvent>(this IEventLog eventLog, Action<TEvent, FilterLog> action)
		where TEvent : IEventDTO
	{
		if (eventLog is EventLog<TEvent> specificEventLog)
		{
			action(specificEventLog.Event, specificEventLog.Log);
		}
	}

	public static Task WhenIsAsync<TEvent>(this IEventLog eventLog, Func<TEvent, FilterLog, CancellationToken, Task> action, CancellationToken cancellationToken = default)
		where TEvent : IEventDTO
	{
		if (eventLog is EventLog<TEvent> specificEventLog)
		{
			return action(specificEventLog.Event, specificEventLog.Log, cancellationToken);
		}

		return Task.CompletedTask;
	}
}