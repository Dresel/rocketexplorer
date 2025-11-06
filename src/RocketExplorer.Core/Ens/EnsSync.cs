using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ENS.ENSRegistry.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Ethereum;

namespace RocketExplorer.Core.Ens;

// TODO Expiration
public class EnsSync(IOptions<SyncOptions> options, GlobalContext globalContext)
	: SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(bool processedBlocks, CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(processedBlocks, cancellationToken);

		EnsContext ensContext = await GlobalContext.EnsContextFactory;

		if (!processedBlocks)
		{
			return;
		}

		EnsUtil ensUtil = new();

		int count = 0;

		// Resolution of new candidates added via node / token sync
		await Parallel.ForEachAsync(
			GlobalContext.Services.AddressEnsProcessHistory.ProcessedAddressEnsCandidates
				.Where(x => x.Ens is null), cancellationToken, async (candidate, _) =>
			{
				int localCount = Interlocked.Increment(ref count);
				if (localCount % 1000 == 0)
				{
					GlobalContext.LoggerFactory.CreateLogger<EnsSync>()
						.LogInformation("{Count} address lookups done", localCount);
				}

				string candidateAddress = candidate.Address.ToHex(true);

				// Address not known anymore (removal)
				if (!ensContext.IsKnownAddress(candidateAddress))
				{
					return;
				}

				if (ensContext.ContainsEnsForAddress(candidateAddress))
				{
					// Already known, no resolution needed, update index entries in next step
					return;
				}

				byte[] reverseAddressNameHash = ensUtil.ToReverseAddressNameHash(candidateAddress);
				ReverseAddressLookupResult reverseAddressLookupResult = await GlobalContext.VerifyAddressAsync(
					reverseAddressNameHash, new HexBigInteger(GlobalContext.LatestBlockHeight));

				if (!reverseAddressLookupResult.IsValidPrimary)
				{
					// Resolution failed
					return;
				}

				ensContext.AddToEnsMaps([(candidate.Address, reverseAddressLookupResult.ReverseResolvedEnsName!),]);
			});

		count = 0;

		// TODO: Verify remove workflow, e.g. no holder anymore

		// Deletions / updates of all candidates
		foreach (AddressEnsRecord candidate in GlobalContext.Services.AddressEnsProcessHistory
					.ProcessedAddressEnsCandidates)
		{
			count++;

			if (count % 1000 == 0)
			{
				GlobalContext.LoggerFactory.CreateLogger<EnsSync>()
					.LogInformation("{Count} addresses indexed", count);
			}

			string candidateAddress = candidate.Address.ToHex(true);
			string? targetEns = ensContext.TryGetEnsNameFromAddress(candidateAddress);

			string? obsoleteEns = null;

			if (ensContext.OldAddressEnsMap.TryGetValue(candidate.Address, out string? obsoleteEnsName) && !ensContext.ContainsEnsName(obsoleteEnsName) && !string.Equals(obsoleteEnsName, targetEns, StringComparison.OrdinalIgnoreCase))
			{
				obsoleteEns = candidate.Ens;
			}

			await GlobalContext.UpdateEnsNameAsync(obsoleteEns, candidate.Address, targetEns, cancellationToken);
		}
	}

	protected override async Task<long> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default)
	{
		EnsContext context = await GlobalContext.EnsContextFactory;
		return context.CurrentBlockHeight;
	}

	protected override async Task HandleBlocksAsync(
		long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		EnsContext context = await GlobalContext.EnsContextFactory;

		IEnumerable<IEventLog> ensEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock,
			[
				typeof(AddrChangedEventDTO), typeof(AddressChangedEventDTO), typeof(NameChangedEventDTO),
				typeof(NewResolverEventDTO), typeof(ResolverChangedEventDTO),
			], [],
			GlobalContext.Policy);

		ConcurrentBag<(IEventLog EventLog, EnsEventHandlerResult Result)> resultBag = [];

		await Parallel.ForEachAsync(
			ensEvents, cancellationToken, async (ensEvent, cancellationTokenInner) =>
			{
				EnsEventHandlerResult? t =
					await ensEvent.WhenIsAsync<AddrChangedEventDTO, GlobalContext, EnsEventHandlerResult?>(
						EnsEventHandlers.HandleAsync, GlobalContext, cancellationTokenInner);

				t ??= await ensEvent.WhenIsAsync<NewResolverEventDTO, GlobalContext, EnsEventHandlerResult?>(
					EnsEventHandlers.HandleAsync, GlobalContext, cancellationTokenInner);

				t ??= await ensEvent
					.WhenIsAsync<ResolverChangedEventDTO, GlobalContext, EnsEventHandlerResult?>(
						EnsEventHandlers.HandleAsync, GlobalContext, cancellationTokenInner);

				t ??= await ensEvent
					.WhenIsAsync<AddressChangedEventDTO, GlobalContext, EnsEventHandlerResult?>(
						EnsEventHandlers.HandleAsync, GlobalContext, cancellationTokenInner);

				t ??= await ensEvent.WhenIsAsync<NameChangedEventDTO, GlobalContext, EnsEventHandlerResult?>(
					EnsEventHandlers.HandleAsync, GlobalContext, cancellationTokenInner);

				if (t is not null)
				{
					resultBag.Add((ensEvent, t));
				}
			});

		foreach ((IEventLog EventLog, EnsEventHandlerResult Result) eventLog in resultBag
					.OrderBy(x => (long)x.EventLog.Log.BlockNumber.Value)
					.ThenBy(x => (long)x.EventLog.Log.LogIndex.Value))
		{
			EnsEventHandlerResult result = eventLog.Result;

			if (result.ReverseResult is not null && result.ReverseResult.ReverseResolver is not null)
			{
				if (context.TryGetAddressFromReverseAddressNameHash(
						result.ReverseResult.AddressReverseNameHash, out string? address))
				{
					await GlobalContext.TryRemoveFromReverseAddressNameHashAsync(
						result.ReverseResult.AddressReverseNameHash, cancellationToken);

					if (result.ReverseResult.IsValidPrimary)
					{
						// New valid primary, remove old forward entry if exists
						await GlobalContext.TryRemoveFromEnsNameHashAsync(
							result.ReverseResult.ReverseResolvedEnsNameHash ??
							throw new InvalidOperationException("ReverseResolvedEnsNameHash must not be null"),
							cancellationToken);

						context.AddToEnsMaps(
							[(address!.HexToByteArray(), result.ReverseResult.ReverseResolvedEnsName!),]);

						await GlobalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
							address.HexToByteArray(),
							result.ReverseResult.ReverseResolvedEnsName, cancellationToken);

						GlobalContext.LoggerFactory.CreateLogger<EnsSync>().LogInformation(
							"Add {EnsName} {Address} ({Block})", result.ReverseResult.ReverseResolvedEnsName, address,
							eventLog.EventLog.Log.BlockNumber);
						continue;
					}
				}
			}

			if (result.ForwardResult is not null)
			{
				// Remove old forward entry if exists
				await GlobalContext.TryRemoveFromEnsNameHashAsync(result.ForwardResult.EnsNameHash, cancellationToken);

				if (!result.ForwardResult.IsValidPrimary ||
					!context.ContainsReverseAddressNameHash(
						result.ForwardResult.ForwardResolvedAddressReverseNameHash!))
				{
					continue;
				}

				// Remove old reverse entries if exists
				await GlobalContext.TryRemoveFromReverseAddressNameHashAsync(
					result.ForwardResult.ForwardResolvedAddressReverseNameHash!, cancellationToken);

				context.AddToEnsMaps(
				[
					(result.ForwardResult.ForwardResolvedAddress!.HexToByteArray(),
						result.ForwardResult.ReverseResolvedEnsName!),
				]);

				await GlobalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
					result.ForwardResult.ForwardResolvedAddress!.HexToByteArray(),
					result.ForwardResult.ReverseResolvedEnsName!, cancellationToken);

				GlobalContext.LoggerFactory.CreateLogger<EnsSync>().LogInformation(
					"Add {EnsName} {Address} ({Block})", result.ForwardResult.ReverseResolvedEnsName,
					result.ForwardResult.ForwardResolvedAddress, eventLog.EventLog.Log.BlockNumber);
			}
		}
	}

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		EnsContext context = await GlobalContext.EnsContextFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}
}