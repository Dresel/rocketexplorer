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
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Core.Ens;

// TODO Expiration
public class EnsSync(IOptions<SyncOptions> options, GlobalContext globalContext)
	: SyncBase(options, globalContext)
{
	protected override async Task AfterHandleBlocksAsync(CancellationToken cancellationToken)
	{
		await base.AfterHandleBlocksAsync(cancellationToken);

		EnsUtil ensUtil = new();
		NodesContext nodesContext = await GlobalContext.NodesContextFactory;
		TokensContext tokensContext = await GlobalContext.TokensContextFactory;
		EnsContext ensContext = await GlobalContext.EnsContextFactory;

		int count = 0;

		// Resolution of new candidates added via node / token sync or initial sync
		//foreach (AddressEnsCandidate candidate in GlobalContext.Services.ProcessHistory.ProcessedAddressEnsCandidates
		//			.Where(x => x.Ens is null))
		await Parallel.ForEachAsync(
			GlobalContext.Services.ProcessHistory.ProcessedAddressEnsCandidates
				.Where(x => x.Ens is null), cancellationToken, async (candidate, _) =>
			{
				int localCount = Interlocked.Increment(ref count);
				if (localCount % 1000 == 0)
				{
					GlobalContext.LoggerFactory.CreateLogger<EnsSync>()
						.LogInformation("{Count} addresses processed", count);
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

		// Deletions / updates of all candidates
		foreach (AddressEnsCandidate candidate in GlobalContext.Services.ProcessHistory.ProcessedAddressEnsCandidates)
		{
			string candidateAddress = candidate.Address.ToHex(true);
			string? candidateEns = candidate.Ens;

			string? targetEns = ensContext.TryGetEnsNameFromAddress(candidateAddress);

			if (candidateEns is not null)
			{
				if (!ensContext.ContainsEnsName(candidateEns))
				{
					// Existing ens entry is obsolete, remove
					_ = GlobalContext.Services.GlobalEnsIndexService.TryRemoveEntryAsync(
						candidateEns, candidateEns, EventIndex.Zero, cancellationToken);
				}
			}

			if (targetEns is null && ensContext.InitialSync)
			{
				// Nothing to delete, continue
				continue;
			}

			if (targetEns is null)
			{
				if (UpdateNode(nodesContext, candidateAddress, null))
				{
					// Update queues
				}

				UpdateTokenInfo(tokensContext.RPLTokenInfo, candidateAddress, null);
				UpdateTokenInfo(tokensContext.RPLOldTokenInfo, candidateAddress, null);
				UpdateTokenInfo(tokensContext.RETHTokenInfo, candidateAddress, null);
				UpdateTokenInfo(tokensContext.RockRETHTokenInfo, candidateAddress, null);

				_ = GlobalContext.Services.GlobalIndexService.UpdateEntryAsync(
					candidateAddress.RemoveHexPrefix(), candidate.Address, EventIndex.Zero,
					entry => entry.AddressEnsName = null,
					cancellationToken: cancellationToken);

				continue;
			}

			bool nodeUpdated = UpdateNode(nodesContext, candidateAddress, targetEns);

			if (nodeUpdated)
			{
				// Update queues
			}

			bool rplUpdated = UpdateTokenInfo(tokensContext.RPLTokenInfo, candidateAddress, targetEns);
			bool rplOldUpdated = UpdateTokenInfo(tokensContext.RPLOldTokenInfo, candidateAddress, targetEns);
			bool rethUpdated = UpdateTokenInfo(tokensContext.RETHTokenInfo, candidateAddress, targetEns);
			bool rockRETHUpdated = UpdateTokenInfo(tokensContext.RockRETHTokenInfo, candidateAddress, targetEns);

			IndexEntryType type =
				(nodeUpdated ? IndexEntryType.NodeOperator : 0) |
				(rplUpdated ? IndexEntryType.RPLHolder : 0) |
				(rplOldUpdated ? IndexEntryType.RPLOldHolder : 0) |
				(rethUpdated ? IndexEntryType.RETHHolder : 0) |
				(rockRETHUpdated ? IndexEntryType.RockRETHHolder : 0);

			if (type == 0)
			{
				throw new InvalidOperationException("Type is not supposed to be 0");
			}

			_ = GlobalContext.Services.GlobalEnsIndexService.AddOrUpdateEntryAsync(
				targetEns, targetEns, EventIndex.Zero, new EnsIndexEntry
				{
					Address = candidate.Address,
					AddressEnsName = targetEns,
					Type = type,
				}, cancellationToken);

			_ = GlobalContext.Services.GlobalIndexService.UpdateEntryAsync(
				candidateAddress.RemoveHexPrefix(), candidate.Address, EventIndex.Zero,
				entry => entry.AddressEnsName = targetEns,
				cancellationToken: cancellationToken);
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

		if (context.InitialSync)
		{
			return;
		}

		IEnumerable<IEventLog> nodeAddedEvents = await GlobalContext.Services.Web3.FilterAsync(
			fromBlock, toBlock,
			[
				typeof(AddrChangedEventDTO), typeof(AddressChangedEventDTO), typeof(NameChangedEventDTO),
				typeof(NewResolverEventDTO), typeof(ResolverChangedEventDTO),
			], [],
			GlobalContext.Policy);

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			await eventLog.WhenIsAsync<AddrChangedEventDTO, GlobalContext>(
				EnsEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<NewResolverEventDTO, GlobalContext>(
				EnsEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<ResolverChangedEventDTO, GlobalContext>(
				EnsEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<AddressChangedEventDTO, GlobalContext>(
				EnsEventHandlers.HandleAsync, GlobalContext, cancellationToken);

			await eventLog.WhenIsAsync<NameChangedEventDTO, GlobalContext>(
				EnsEventHandlers.HandleAsync, GlobalContext, cancellationToken);
		}
	}

	protected override async Task SetCurrentBlockHeightAsync(
		long currentBlockHeight, CancellationToken cancellationToken = default)
	{
		EnsContext context = await GlobalContext.EnsContextFactory;
		context.CurrentBlockHeight = currentBlockHeight;
	}

	private bool UpdateNode(NodesContext nodesContext, string candidateAddress, string? ensName)
	{
		if (nodesContext.Nodes.Data.Index.TryGetValue(candidateAddress, out NodeIndexEntry? nodeIndexEntry))
		{
			nodesContext.Nodes.Data.Index[candidateAddress] = nodeIndexEntry with
			{
				ContractAddressEnsName = ensName,
			};

			return true;
		}

		return false;
	}

	private bool UpdateTokenInfo(TokenInfo tokenInfo, string candidateAddress, string? ensName)
	{
		if (tokenInfo.Holders.TryGetValue(candidateAddress, out HolderEntry? tokenIndexEntry))
		{
			tokenInfo.Holders[candidateAddress] = tokenIndexEntry with
			{
				AddressEnsName = ensName,
			};

			return true;
		}

		return false;
	}
}