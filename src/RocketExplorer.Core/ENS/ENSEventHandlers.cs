using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS.ENSRegistry.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexTypes;
using RocketExplorer.Core.Nodes;

namespace RocketExplorer.Core.ENS;

public static class ENSEventHandlers
{
	public static async Task HandleAsync(
		this ENSSyncContext context, EventLog<AddrChangedEventDTO> eventLog, CancellationToken cancellationToken) =>
		await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber);

	public static async Task HandleAsync(
		this ENSSyncContext context, EventLog<NewResolverEventDTO> eventLog, CancellationToken cancellationToken)
	{
		if ((await context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber)).ReverseResolver is null)
		{
			await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber);
		}
	}

	public static async Task HandleAsync(
		this ENSSyncContext context, EventLog<ResolverChangedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		if ((await context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber)).ReverseResolver is null)
		{
			await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber);
		}
	}

	public static async Task HandleAsync(
		this ENSSyncContext context, EventLog<AddressChangedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		if (eventLog.Event.CoinType == 60)
		{
			await HandleForward(context, eventLog.Event.Node, eventLog.Log.BlockNumber);
		}
	}

	public static Task HandleAsync(
		this ENSSyncContext context, EventLog<NameChangedEventDTO> eventLog, CancellationToken cancellationToken)
		=> context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber);

	private static async Task<EnsNameLookupResult> HandleForward(
		this ENSSyncContext context, byte[] ensNameHash, HexBigInteger blockNumber)
	{
		// Remove old forward entry if exists
		context.TryRemoveFromEnsNameHash(ensNameHash);

		EnsNameLookupResult ensNameLookupResult = await context.VerifyEnsName(ensNameHash, blockNumber);

		if (!ensNameLookupResult.IsValidPrimary || !context.IndexReverseAddressNameHashMap.Contains(ensNameLookupResult.ForwardResolvedAddressReverseNameHash!))
		{
			return ensNameLookupResult;
		}

		// Remove old reverse entries if exists
		context.TryRemoveFromReverseAddressNameHash(ensNameLookupResult.ForwardResolvedAddressReverseNameHash!);

		context.AddressToEnsNameHash.Add(ensNameLookupResult.ForwardResolvedAddress!, ensNameLookupResult.EnsNameHash!);
		context.AddressToReverseAddressNameHash.Add(ensNameLookupResult.ForwardResolvedAddress!, ensNameLookupResult.ForwardResolvedAddressReverseNameHash!);
		context.EnsNameToEnsNameHash.Add(ensNameLookupResult.ReverseResolvedEnsName!, ensNameLookupResult.EnsNameHash);

		context.Logger.LogInformation("Add {EnsName} {Address}", ensNameLookupResult.ReverseResolvedEnsName!, ensNameLookupResult.ForwardResolvedAddress!);

		return ensNameLookupResult;
	}

	private static async Task<ReverseAddressLookupResult> HandleReverseAsync(
		this ENSSyncContext context, byte[] reverseAddressNameHashCandidate, HexBigInteger blockNumber)
	{
		if (!context.IndexReverseAddressNameHashMap.TryGetValue(reverseAddressNameHashCandidate, out string? address))
		{
			return new ReverseAddressLookupResult()
			{
				AddressReverseNameHash = reverseAddressNameHashCandidate,
				ReverseResolver = null,
				ReverseResolvedEnsName = null,
				ReverseResolvedEnsNameHash = null,
				ForwardResolver = null,
				ForwardResolvedAddressReverseNameHash = null,
			};
		}

		ReverseAddressLookupResult addressResult = await context.VerifyAddress(
			reverseAddressNameHashCandidate, blockNumber);

		// Remove old reverse entries if exists
		context.TryRemoveFromReverseAddressNameHash(addressResult.AddressReverseNameHash);

		if (!addressResult.IsValidPrimary)
		{
			return addressResult;
		}

		// New valid primary, remove old forward entry if exists
		context.TryRemoveFromEnsNameHash(
			addressResult.ReverseResolvedEnsNameHash ??
			throw new InvalidOperationException("ReverseResolvedEnsNameHash must not be null"));

		context.AddressToEnsNameHash.Add(address!, addressResult.ReverseResolvedEnsNameHash);
		context.AddressToReverseAddressNameHash.Add(address!, addressResult.AddressReverseNameHash);
		context.EnsNameToEnsNameHash.Add(addressResult.ReverseResolvedEnsName!, addressResult.ReverseResolvedEnsNameHash);

		context.Logger.LogInformation("Add {EnsName} {Address}", addressResult.ReverseResolvedEnsName, address);

		return addressResult;
	}
}