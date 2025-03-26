using System.Diagnostics;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Polly.Retry;

namespace RocketExplorer.Ethereum;

public static class Web3Extensions
{
	public static async Task<IEnumerable<IEventLog>> FilterAsync(
	this Web3 web3, ulong fromBlock, ulong toBlock, ICollection<Type> eventDtoTypes, ICollection<string> contractAddresses, AsyncRetryPolicy policy)
	{
		Debug.Assert(eventDtoTypes.All(eventDtoType => typeof(IEventDTO).IsAssignableFrom(eventDtoType)), "eventDtoTypes must contain IEventDTO types");

		IEthApiContractService ethApiContractService = web3.Eth;
		PropertyInfo eventAbiProperty = typeof(EventBase).GetProperty(nameof(EventBase.EventABI)) ?? throw new InvalidOperationException("EventABI property not found");

		List<EventBase> events = [];
		List<string> eventSignatures = [];

		foreach (Type eventDtoType in eventDtoTypes)
		{
			EventBase @event = typeof(IEthApiContractService).GetMethod(nameof(IEthApiContractService.GetEvent), BindingFlags.Public | BindingFlags.Instance, [])?
				.MakeGenericMethod(eventDtoType).Invoke(ethApiContractService, []) as EventBase ?? throw new InvalidOperationException("GetEvent method not found or null returned");

			events.Add(@event);

			EventABI eventAbi = (EventABI)(eventAbiProperty.GetValue(@event) ?? throw new InvalidOperationException("EventABI should not return null"));
			eventSignatures.Add(eventAbi.Signature.Sha3().ToHex(true));
		}

		NewFilterInput filter = new()
		{
			FromBlock = new BlockParameter(fromBlock),
			ToBlock = new BlockParameter(toBlock),
			Topics =
			[
				eventSignatures.ToArray(),
			],
			Address = contractAddresses.ToArray(),
		};

		FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));

		IEnumerable<IEventLog> results = events.SelectMany(
			eventType => (IEnumerable<IEventLog>)(eventType.GetType().GetMethod(
					nameof(Event.DecodeAllEventsForEvent), BindingFlags.Public | BindingFlags.Instance,
					[typeof(FilterLog[])])?.Invoke(eventType, [logs]) ??
				throw new InvalidOperationException("DecodeAllEventsForEvent method not found or null returned")));

		return results.OrderBy(x => (ulong)x.Log.BlockNumber.Value);


		//try
		//{
		//	Type type = typeof(TEventType1);
		//	IEthApiContractService ethApiContractService = web3.Eth;

		//	MethodInfo methodInfo = typeof(IEthApiContractService).GetMethod(nameof(IEthApiContractService.GetEvent), BindingFlags.Public | BindingFlags.Instance, []).MakeGenericMethod(type);
		//	object eventType = methodInfo.Invoke(ethApiContractService, []);

		//	EventABI value = (EventABI)typeof(EventBase).GetProperty(nameof(EventBase.EventABI)).GetValue(eventType);
		//	string hex = value.Signature.Sha3().ToHex(true);

		//	NewFilterInput filter = new()
		//	{
		//		FromBlock = new BlockParameter(fromBlock),
		//		ToBlock = new BlockParameter(toBlock),
		//		Topics =
		//		[
		//			new[]
		//			{
		//				hex
		//			},
		//		],
		//	};

		//	FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));

		//	IEnumerable<IEventLog> results = (IEnumerable<IEventLog>)eventType.GetType().GetMethod(nameof(Event.DecodeAllEventsForEvent), BindingFlags.Public | BindingFlags.Instance, [typeof(FilterLog[])]).Invoke(eventType, [logs]);
		//}
		//catch (Exception e)
		//{
		//	throw;
		//}

		//throw new NotImplementedException();

		//Event<TEventType1> event1 = web3.Eth.GetEvent<TEventType1>();
		//Event<TEventType2> event2 = web3.Eth.GetEvent<TEventType2>();
		//Event<TEventType3> event3 = web3.Eth.GetEvent<TEventType3>();
		//Event<TEventType4> event4 = web3.Eth.GetEvent<TEventType4>();



		//NewFilterInput filter = new()
		//{
		//	FromBlock = new BlockParameter(fromBlock),
		//	ToBlock = new BlockParameter(toBlock),
		//	Topics =
		//	[
		//		new[]
		//		{
		//			event1.EventABI.Signature.Sha3().ToHex(true), event2.EventABI.Signature.Sha3().ToHex(true),
		//			event3.EventABI.Signature.Sha3().ToHex(true), event4.EventABI.Signature.Sha3().ToHex(true),
		//		},
		//	],
		//};

		//FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));

		//return event1.DecodeAllEventsForEvent(logs).Cast<IEventLog>()
		//	.Concat(event2.DecodeAllEventsForEvent(logs))
		//	.Concat(event3.DecodeAllEventsForEvent(logs))
		//	.Concat(event4.DecodeAllEventsForEvent(logs))
		//	.OrderBy(x => (ulong)x.Log.BlockNumber.Value);
	}


	public static async Task<IEnumerable<IEventLog>> FilterAsync<TEventType1>(
		this Web3 web3, ulong fromBlock, ulong toBlock, AsyncRetryPolicy policy)
		where TEventType1 : IEventDTO, new()
	{
		Event<TEventType1> event1 = web3.Eth.GetEvent<TEventType1>();

		NewFilterInput filter = new()
		{
			FromBlock = new BlockParameter(fromBlock),
			ToBlock = new BlockParameter(toBlock),
			Topics =
			[
				new[] { event1.EventABI.Signature.Sha3().ToHex(true), },
			],
		};

		FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));

		return event1.DecodeAllEventsForEvent(logs)
			.OrderBy(x => (ulong)x.Log.BlockNumber.Value);
	}

	public static async Task<IEnumerable<IEventLog>> FilterAsync<TEventType1, TEventType2>(
		this Web3 web3, ulong fromBlock, ulong toBlock, AsyncRetryPolicy policy)
		where TEventType1 : IEventDTO, new()
		where TEventType2 : IEventDTO, new()
	{
		Event<TEventType1> event1 = web3.Eth.GetEvent<TEventType1>();
		Event<TEventType2> event2 = web3.Eth.GetEvent<TEventType2>();

		NewFilterInput filter = new()
		{
			FromBlock = new BlockParameter(fromBlock),
			ToBlock = new BlockParameter(toBlock),
			Topics =
			[
				new[] { event1.EventABI.Signature.Sha3().ToHex(true), event2.EventABI.Signature.Sha3().ToHex(true), },
			],
		};

		FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));
		return event1.DecodeAllEventsForEvent(logs).Cast<IEventLog>()
			.Concat(event2.DecodeAllEventsForEvent(logs))
			.OrderBy(x => (ulong)x.Log.BlockNumber.Value);
	}

	public static async Task<IEnumerable<IEventLog>> FilterAsync<TEventType1, TEventType2, TEventType3>(
		this Web3 web3, ulong fromBlock, ulong toBlock, AsyncRetryPolicy policy)
		where TEventType1 : IEventDTO, new()
		where TEventType2 : IEventDTO, new()
		where TEventType3 : IEventDTO, new()
	{
		Event<TEventType1> event1 = web3.Eth.GetEvent<TEventType1>();
		Event<TEventType2> event2 = web3.Eth.GetEvent<TEventType2>();
		Event<TEventType3> event3 = web3.Eth.GetEvent<TEventType3>();

		NewFilterInput filter = new()
		{
			FromBlock = new BlockParameter(fromBlock),
			ToBlock = new BlockParameter(toBlock),
			Topics =
			[
				new[]
				{
					event1.EventABI.Signature.Sha3().ToHex(true), event2.EventABI.Signature.Sha3().ToHex(true),
					event3.EventABI.Signature.Sha3().ToHex(true),
				},
			],
		};

		FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));
		return event1.DecodeAllEventsForEvent(logs).Cast<IEventLog>()
			.Concat(event2.DecodeAllEventsForEvent(logs))
			.Concat(event3.DecodeAllEventsForEvent(logs))
			.OrderBy(x => (ulong)x.Log.BlockNumber.Value);
	}

	public static async Task<IEnumerable<IEventLog>> FilterAsync<TEventType1, TEventType2, TEventType3, TEventType4>(
		this Web3 web3, ulong fromBlock, ulong toBlock, AsyncRetryPolicy policy)
		where TEventType1 : IEventDTO, new()
		where TEventType2 : IEventDTO, new()
		where TEventType3 : IEventDTO, new()
		where TEventType4 : IEventDTO, new()
	{
		Event<TEventType1> event1 = web3.Eth.GetEvent<TEventType1>();
		Event<TEventType2> event2 = web3.Eth.GetEvent<TEventType2>();
		Event<TEventType3> event3 = web3.Eth.GetEvent<TEventType3>();
		Event<TEventType4> event4 = web3.Eth.GetEvent<TEventType4>();

		NewFilterInput filter = new()
		{
			FromBlock = new BlockParameter(fromBlock),
			ToBlock = new BlockParameter(toBlock),
			Topics =
			[
				new[]
				{
					event1.EventABI.Signature.Sha3().ToHex(true), event2.EventABI.Signature.Sha3().ToHex(true),
					event3.EventABI.Signature.Sha3().ToHex(true), event4.EventABI.Signature.Sha3().ToHex(true),
				},
			],
		};

		FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));

		return event1.DecodeAllEventsForEvent(logs).Cast<IEventLog>()
			.Concat(event2.DecodeAllEventsForEvent(logs))
			.Concat(event3.DecodeAllEventsForEvent(logs))
			.Concat(event4.DecodeAllEventsForEvent(logs))
			.OrderBy(x => (ulong)x.Log.BlockNumber.Value);
	}

	public static async Task<IEnumerable<IEventLog>> FilterAsync<TEventType1, TEventType2, TEventType3, TEventType4,
		TEventType5>(this Web3 web3, ulong fromBlock, ulong toBlock, AsyncRetryPolicy policy)
		where TEventType1 : IEventDTO, new()
		where TEventType2 : IEventDTO, new()
		where TEventType3 : IEventDTO, new()
		where TEventType4 : IEventDTO, new()
		where TEventType5 : IEventDTO, new()
	{
		Event<TEventType1> event1 = web3.Eth.GetEvent<TEventType1>();
		Event<TEventType2> event2 = web3.Eth.GetEvent<TEventType2>();
		Event<TEventType3> event3 = web3.Eth.GetEvent<TEventType3>();
		Event<TEventType4> event4 = web3.Eth.GetEvent<TEventType4>();
		Event<TEventType5> event5 = web3.Eth.GetEvent<TEventType5>();

		NewFilterInput filter = new()
		{
			FromBlock = new BlockParameter(fromBlock),
			ToBlock = new BlockParameter(toBlock),
			Topics =
			[
				new[]
				{
					event1.EventABI.Signature.Sha3().ToHex(true), event2.EventABI.Signature.Sha3().ToHex(true),
					event3.EventABI.Signature.Sha3().ToHex(true), event4.EventABI.Signature.Sha3().ToHex(true),
					event5.EventABI.Signature.Sha3().ToHex(true),
				},
			],
		};

		FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));
		return event1.DecodeAllEventsForEvent(logs).Cast<IEventLog>()
			.Concat(event2.DecodeAllEventsForEvent(logs))
			.Concat(event3.DecodeAllEventsForEvent(logs))
			.Concat(event4.DecodeAllEventsForEvent(logs))
			.Concat(event5.DecodeAllEventsForEvent(logs))
			.OrderBy(x => (ulong)x.Log.BlockNumber.Value);
	}

	public static async Task<IEnumerable<IEventLog>> FilterAsync<TEventType1, TEventType2, TEventType3, TEventType4,
		TEventType5, TEventType6>(this Web3 web3, ulong fromBlock, ulong toBlock, AsyncRetryPolicy policy)
		where TEventType1 : IEventDTO, new()
		where TEventType2 : IEventDTO, new()
		where TEventType3 : IEventDTO, new()
		where TEventType4 : IEventDTO, new()
		where TEventType5 : IEventDTO, new()
		where TEventType6 : IEventDTO, new()
	{
		Event<TEventType1> event1 = web3.Eth.GetEvent<TEventType1>();
		Event<TEventType2> event2 = web3.Eth.GetEvent<TEventType2>();
		Event<TEventType3> event3 = web3.Eth.GetEvent<TEventType3>();
		Event<TEventType4> event4 = web3.Eth.GetEvent<TEventType4>();
		Event<TEventType5> event5 = web3.Eth.GetEvent<TEventType5>();
		Event<TEventType6> event6 = web3.Eth.GetEvent<TEventType6>();

		NewFilterInput filter = new()
		{
			FromBlock = new BlockParameter(fromBlock),
			ToBlock = new BlockParameter(toBlock),
			Topics =
			[
				new[]
				{
					event1.EventABI.Signature.Sha3().ToHex(true), event2.EventABI.Signature.Sha3().ToHex(true),
					event3.EventABI.Signature.Sha3().ToHex(true), event4.EventABI.Signature.Sha3().ToHex(true),
					event5.EventABI.Signature.Sha3().ToHex(true), event6.EventABI.Signature.Sha3().ToHex(true),
				},
			],
		};

		FilterLog[]? logs = await policy.ExecuteAsync(() => web3.Eth.Filters.GetLogs.SendRequestAsync(filter));
		return event1.DecodeAllEventsForEvent(logs).Cast<IEventLog>()
			.Concat(event2.DecodeAllEventsForEvent(logs))
			.Concat(event3.DecodeAllEventsForEvent(logs))
			.Concat(event4.DecodeAllEventsForEvent(logs))
			.Concat(event5.DecodeAllEventsForEvent(logs))
			.Concat(event6.DecodeAllEventsForEvent(logs))
			.OrderBy(x => (ulong)x.Log.BlockNumber.Value);
	}
}