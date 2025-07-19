using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Core.Nodes;

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

	public static Task WhenIsAsync<TEvent>(
		this IEventLog eventLog, Func<TEvent, FilterLog, CancellationToken, Task> action,
		CancellationToken cancellationToken = default)
		where TEvent : IEventDTO
	{
		if (eventLog is EventLog<TEvent> specificEventLog)
		{
			return action(specificEventLog.Event, specificEventLog.Log, cancellationToken);
		}

		return Task.CompletedTask;
	}

	public static Task<TResult?> WhenIsAsync<TEvent, TResult>(
		this IEventLog eventLog, Func<TEvent, FilterLog, CancellationToken, Task<TResult?>> action,
		CancellationToken cancellationToken = default)
		where TEvent : IEventDTO
	{
		if (eventLog is EventLog<TEvent> specificEventLog)
		{
			return action(specificEventLog.Event, specificEventLog.Log, cancellationToken);
		}

		return Task.FromResult(default(TResult));
	}

	public static async Task WhenIsAsync<TEvent>(
		this IEventLog eventLog, Func<TEvent, FilterLog, CancellationToken, Task>[] actions,
		CancellationToken cancellationToken = default)
		where TEvent : IEventDTO
	{
		if (eventLog is EventLog<TEvent> specificEventLog)
		{
			foreach (Func<TEvent, FilterLog, CancellationToken, Task> action in actions)
			{
				await action(specificEventLog.Event, specificEventLog.Log, cancellationToken);
			}
		}
	}

	public static Task WhenIsAsync<TEvent>(
		this IEventLog eventLog, Func<NodesSyncContext, EventLog<TEvent>, CancellationToken, Task> action,
		NodesSyncContext context, CancellationToken cancellationToken = default)
		where TEvent : IEventDTO
	{
		if (eventLog is EventLog<TEvent> specificEventLog)
		{
			return action(context, specificEventLog, cancellationToken);
		}

		return Task.CompletedTask;
	}
}