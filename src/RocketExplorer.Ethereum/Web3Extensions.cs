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
	}
}