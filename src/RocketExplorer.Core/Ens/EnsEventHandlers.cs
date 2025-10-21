using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS.ENSRegistry.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;

namespace RocketExplorer.Core.Ens;

public static class EnsEventHandlers
{
	//public static void AddEnsResult(
	//	EnsContext context, string address, byte[] addressReverseNameHash, string ens, byte[] ensNameHash)
	//{
	//	context.AddressToEnsNameHash.Add(address, ensNameHash);
	//	context.AddressToReverseAddressNameHash.Add(address, addressReverseNameHash);
	//	context.EnsNameToEnsNameHash.Add(ens, ensNameHash);
	//}

	public static async Task HandleAsync(
		this GlobalContext context, EventLog<AddrChangedEventDTO> eventLog, CancellationToken cancellationToken) =>
		await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber);

	public static async Task HandleAsync(
		this GlobalContext context, EventLog<NewResolverEventDTO> eventLog, CancellationToken cancellationToken)
	{
		if ((await context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber)).ReverseResolver is null)
		{
			await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber);
		}
	}

	public static async Task HandleAsync(
		this GlobalContext context, EventLog<ResolverChangedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		if ((await context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber)).ReverseResolver is null)
		{
			await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber);
		}
	}

	public static async Task HandleAsync(
		this GlobalContext context, EventLog<AddressChangedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		if (eventLog.Event.CoinType == 60)
		{
			await HandleForward(context, eventLog.Event.Node, eventLog.Log.BlockNumber);
		}
	}

	public static Task HandleAsync(
		this GlobalContext context, EventLog<NameChangedEventDTO> eventLog, CancellationToken cancellationToken)
		=> context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber);

	private static async Task<EnsNameLookupResult> HandleForward(
		this GlobalContext globalContext, byte[] ensNameHash, HexBigInteger blockNumber)
	{
		EnsContext context = await globalContext.EnsContextFactory;

		// Remove old forward entry if exists
		await globalContext.TryRemoveFromEnsNameHashAsync(ensNameHash);

		// TODO: Verify cost vs event scan, if this is heavy parallelize
		EnsNameLookupResult ensNameLookupResult = await globalContext.VerifyEnsNameAsync(ensNameHash, blockNumber);

		if (!ensNameLookupResult.IsValidPrimary ||
			!context.ContainsReverseAddressNameHash(ensNameLookupResult.ForwardResolvedAddressReverseNameHash!))
		{
			return ensNameLookupResult;
		}

		// Remove old reverse entries if exists
		await globalContext.TryRemoveFromReverseAddressNameHashAsync(ensNameLookupResult.ForwardResolvedAddressReverseNameHash!);

		context.AddToEnsMaps([(ensNameLookupResult.ForwardResolvedAddress!.HexToByteArray(), ensNameLookupResult.ReverseResolvedEnsName!)]);

		await globalContext.Services.ProcessHistory.AddAddressEnsCandidateAsync(
			ensNameLookupResult.ForwardResolvedAddress!.HexToByteArray(),
			ensNameLookupResult.ReverseResolvedEnsName!);

		globalContext.LoggerFactory.CreateLogger<EnsSync>().LogInformation(
			"Add {EnsName} {Address}", ensNameLookupResult.ReverseResolvedEnsName!,
			ensNameLookupResult.ForwardResolvedAddress!);

		return ensNameLookupResult;
	}

	private static async Task<ReverseAddressLookupResult> HandleReverseAsync(
		this GlobalContext globalContext, byte[] reverseAddressNameHashCandidate, HexBigInteger blockNumber)
	{
		EnsContext context = await globalContext.EnsContextFactory;

		if (!context.TryGetAddressFromReverseAddressNameHash(reverseAddressNameHashCandidate, out string? address))
		{
			return new ReverseAddressLookupResult
			{
				AddressReverseNameHash = reverseAddressNameHashCandidate,
				ReverseResolver = null,
				ReverseResolvedEnsName = null,
				ReverseResolvedEnsNameHash = null,
				ForwardResolver = null,
				ForwardResolvedAddressReverseNameHash = null,
			};
		}

		ReverseAddressLookupResult addressResult = await globalContext.VerifyAddressAsync(
			reverseAddressNameHashCandidate, blockNumber);

		// Remove old reverse entries if exists
		await globalContext.TryRemoveFromReverseAddressNameHashAsync(addressResult.AddressReverseNameHash);

		if (!addressResult.IsValidPrimary)
		{
			return addressResult;
		}

		// New valid primary, remove old forward entry if exists
		await globalContext.TryRemoveFromEnsNameHashAsync(
			addressResult.ReverseResolvedEnsNameHash ??
			throw new InvalidOperationException("ReverseResolvedEnsNameHash must not be null"));

		context.AddToEnsMaps([(address!.HexToByteArray(), addressResult.ReverseResolvedEnsName!)]);

		////context.AddressToEnsNameHash.Add(address!, addressResult.ReverseResolvedEnsNameHash);
		////context.AddressToReverseAddressNameHash.Add(address!, addressResult.AddressReverseNameHash);
		////context.EnsNameToEnsNameHash.Add(addressResult.ReverseResolvedEnsName!, addressResult.ReverseResolvedEnsNameHash);

		globalContext.LoggerFactory.CreateLogger<EnsSync>().LogInformation(
			"Add {EnsName} {Address}", addressResult.ReverseResolvedEnsName, address);

		return addressResult;
	}
}