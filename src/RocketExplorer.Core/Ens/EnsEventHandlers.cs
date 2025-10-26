using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ENS.ENSRegistry.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexTypes;

namespace RocketExplorer.Core.Ens;

public static class EnsEventHandlers
{
	public static async Task<EnsEventHandlerResult?> HandleAsync(
		this GlobalContext context, EventLog<AddrChangedEventDTO> eventLog, CancellationToken cancellationToken) =>
		await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber);

	public static async Task<EnsEventHandlerResult?> HandleAsync(
		this GlobalContext context, EventLog<NewResolverEventDTO> eventLog, CancellationToken cancellationToken) =>
		new()
		{
			ReverseResult = (await context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber)).ReverseResult,
			ForwardResult = (await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber)).ForwardResult,
		};

	public static async Task<EnsEventHandlerResult?> HandleAsync(
		this GlobalContext context, EventLog<ResolverChangedEventDTO> eventLog, CancellationToken cancellationToken) =>
		new()
		{
			ReverseResult = (await context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber)).ReverseResult,
			ForwardResult = (await context.HandleForward(eventLog.Event.Node, eventLog.Log.BlockNumber)).ForwardResult,
		};

	public static async Task<EnsEventHandlerResult?> HandleAsync(
		this GlobalContext context, EventLog<AddressChangedEventDTO> eventLog, CancellationToken cancellationToken)
	{
		if (eventLog.Event.CoinType == 60)
		{
			return await HandleForward(context, eventLog.Event.Node, eventLog.Log.BlockNumber);
		}

		return null;
	}

	public static async Task<EnsEventHandlerResult?> HandleAsync(
		this GlobalContext context, EventLog<NameChangedEventDTO> eventLog, CancellationToken cancellationToken)
		=> await context.HandleReverseAsync(eventLog.Event.Node, eventLog.Log.BlockNumber);

	private static async Task<EnsEventHandlerResult> HandleForward(
		this GlobalContext globalContext, byte[] ensNameHash, HexBigInteger blockNumber) =>
		new()
		{
			ForwardResult = await globalContext.VerifyEnsNameAsync(ensNameHash, blockNumber),
		};

	private static async Task<EnsEventHandlerResult> HandleReverseAsync(
		this GlobalContext globalContext, byte[] reverseAddressNameHashCandidate, HexBigInteger blockNumber)
	{
		EnsContext context = await globalContext.EnsContextFactory;

		// Fast path, skip address verification if it's an unknown address
		if (!context.TryGetAddressFromReverseAddressNameHash(reverseAddressNameHashCandidate, out string? _))
		{
			return new EnsEventHandlerResult
			{
				ReverseResult = new ReverseAddressLookupResult
				{
					AddressReverseNameHash = reverseAddressNameHashCandidate,
					ReverseResolver = null,
					ReverseResolvedEnsName = null,
					ReverseResolvedEnsNameHash = null,
					ForwardResolver = null,
					ForwardResolvedAddressReverseNameHash = null,
				},
			};
		}

		return new EnsEventHandlerResult
		{
			ReverseResult = await globalContext.VerifyAddressAsync(reverseAddressNameHashCandidate, blockNumber),
		};
	}
}