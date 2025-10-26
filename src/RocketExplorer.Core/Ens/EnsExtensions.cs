using Microsoft.Extensions.Logging;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Tokens;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Ens;

public static class EnsExtensions
{
	public static async Task<ForwardResolutionResult> TryForwardResolutionAsync(
		this GlobalContext globalContext, byte[] ensNameHash, HexBigInteger blockNumber)
	{
		EnsUtil util = new();
		string? forwardResolverAddress;

		try
		{
			forwardResolverAddress = await globalContext.Services.Web3.Eth.GetEnsService().ENSRegistryService
				.ResolverQueryAsync(ensNameHash, new BlockParameter(blockNumber));
		}
		catch
		{
			return new ForwardResolutionResult
			{
				EnsNameHash = ensNameHash,
				ForwardResolver = null,
				ResolvedAddress = null,
				ResolvedAddressReverseNameHash = null,
			};
		}

		if (forwardResolverAddress.IsNullOrZeroAddress())
		{
			return new ForwardResolutionResult
			{
				EnsNameHash = ensNameHash,
				ForwardResolver = null,
				ResolvedAddress = null,
				ResolvedAddressReverseNameHash = null,
			};
		}

		PublicResolverService forwardResolver = new(globalContext.Services.Web3.Eth, forwardResolverAddress);

		string? forwardAddress;

		try
		{
			forwardAddress = await forwardResolver.AddrQueryAsync(ensNameHash, new BlockParameter(blockNumber));
		}
		catch
		{
			return new ForwardResolutionResult
			{
				EnsNameHash = ensNameHash,
				ForwardResolver = forwardResolver,
				ResolvedAddress = null,
				ResolvedAddressReverseNameHash = null,
			};
		}

		byte[] forwardAddressReverseNameHash = [];

		if (!forwardAddress.IsNullOrZeroAddress())
		{
			forwardAddressReverseNameHash = util.ToReverseAddressNameHash(forwardAddress);
		}

		return new ForwardResolutionResult
		{
			EnsNameHash = ensNameHash,
			ForwardResolver = forwardResolver,
			ResolvedAddress = forwardAddress,
			ResolvedAddressReverseNameHash = forwardAddressReverseNameHash,
		};
	}

	public static async Task TryRemoveFromEnsNameHashAsync(
		this GlobalContext globalContext, byte[] ensNameHash, CancellationToken cancellationToken = default)
	{
		EnsContext context = await globalContext.EnsContextFactory;

		(string RemovedAddress, string RemovedEnsName)? result = context.TryRemoveFromEnsNameHash(ensNameHash);

		if (result is not null)
		{
			globalContext.LoggerFactory.CreateLogger<EnsSync>().LogWarning(
				"Removed entries from reverse record {Ens} => {Address}", result.Value.RemovedEnsName,
				result.Value.RemovedAddress);

			await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
				result.Value.RemovedAddress.HexToByteArray(), result.Value.RemovedEnsName, cancellationToken);
		}
	}

	public static async Task TryRemoveFromReverseAddressNameHashAsync(
		this GlobalContext globalContext, byte[] reverseAddressNameHash, CancellationToken cancellationToken = default)
	{
		EnsContext context = await globalContext.EnsContextFactory;

		(string RemovedAddress, string RemovedEns)? result =
			context.TryRemoveFromReverseAddressNameHash(reverseAddressNameHash);

		if (result is not null)
		{
			globalContext.LoggerFactory.CreateLogger<EnsSync>().LogWarning(
				"Removed entries from reverse record {Address} => {Ens}", result.Value.RemovedAddress,
				result.Value.RemovedEns);

			await globalContext.Services.AddressEnsProcessHistory.AddAddressEnsRecordAsync(
				result.Value.RemovedAddress.HexToByteArray(), result.Value.RemovedEns, cancellationToken);
		}
	}

	public static async Task<EnsReverseResult> TryReverseResolutionAsync(
		this GlobalContext globalContext, byte[] reverseAddressNameHash, HexBigInteger blockNumber)
	{
		EnsUtil ensUtil = new();

		string? reverseResolverAddress = await globalContext.Services.Web3.Eth.GetEnsService().ENSRegistryService
			.ResolverQueryAsync(reverseAddressNameHash, new BlockParameter(blockNumber));

		if (reverseResolverAddress.IsNullOrZeroAddress())
		{
			return new EnsReverseResult
			{
				ReverseNameHash = reverseAddressNameHash,
				ReverseResolver = null,
				ResolvedEnsName = null,
				ResolvedEnsNameHash = null,
			};
		}

		PublicResolverService reverseResolver = new(globalContext.Services.Web3.Eth, reverseResolverAddress);

		string? ensName;

		try
		{
			ensName = await reverseResolver.NameQueryAsync(reverseAddressNameHash, new BlockParameter(blockNumber));
		}
		catch
		{
			return new EnsReverseResult
			{
				ReverseNameHash = reverseAddressNameHash,
				ReverseResolver = reverseResolver,
				ResolvedEnsName = null,
				ResolvedEnsNameHash = null,
			};
		}

		byte[] ensNameHash = [];

		try
		{
			ensNameHash = ensUtil.GetNameHash(ensName).HexToByteArray();
		}
		catch
		{
			globalContext.LoggerFactory.CreateLogger<EnsSync>().LogDebug("Invalid ens name");
		}

		return new EnsReverseResult
		{
			ReverseNameHash = reverseAddressNameHash,
			ReverseResolver = reverseResolver,
			ResolvedEnsName = ensName,
			ResolvedEnsNameHash = ensNameHash,
		};
	}

	public static async Task UpdateEnsNameAsync(
		this GlobalContext globalContext,
		string? obsoleteEnsName, byte[] address, string? ensName, CancellationToken cancellationToken = default)
	{
		NodesContext nodesContext = await globalContext.NodesContextFactory;
		TokensContext tokensContext = await globalContext.TokensContextFactory;

		string candidateAddress = address.ToHex(true);

		if (obsoleteEnsName is not null)
		{
			// Existing ens entry is obsolete, remove
			_ = globalContext.Services.GlobalEnsIndexService.TryRemoveEntryAsync(
				obsoleteEnsName[..^4], obsoleteEnsName, EventIndex.Zero, cancellationToken);
		}

		if (ensName is null)
		{
			await UpdateNodeAsync(
				nodesContext, globalContext.Services.Storage, candidateAddress, null, cancellationToken);
			UpdateTokenInfo(tokensContext.RPLTokenInfo, candidateAddress, null);
			UpdateTokenInfo(tokensContext.RPLOldTokenInfo, candidateAddress, null);
			UpdateTokenInfo(tokensContext.RETHTokenInfo, candidateAddress, null);
			UpdateTokenInfo(tokensContext.RockRETHTokenInfo, candidateAddress, null);

			_ = globalContext.Services.GlobalIndexService.UpdateEntryAsync(
				candidateAddress.RemoveHexPrefix(), address, EventIndex.Zero,
				entry => entry.AddressEnsName = null,
				cancellationToken: cancellationToken);

			return;
		}

		bool nodeUpdated = await UpdateNodeAsync(
			nodesContext, globalContext.Services.Storage, candidateAddress, ensName, cancellationToken);
		bool rplUpdated = UpdateTokenInfo(tokensContext.RPLTokenInfo, candidateAddress, ensName);
		bool rplOldUpdated = UpdateTokenInfo(tokensContext.RPLOldTokenInfo, candidateAddress, ensName);
		bool rethUpdated = UpdateTokenInfo(tokensContext.RETHTokenInfo, candidateAddress, ensName);
		bool rockRETHUpdated = UpdateTokenInfo(tokensContext.RockRETHTokenInfo, candidateAddress, ensName);

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

		_ = globalContext.Services.GlobalEnsIndexService.AddOrUpdateEntryAsync(
			ensName[..^4], ensName, EventIndex.Zero, new EnsIndexEntry
			{
				Address = address,
				AddressEnsName = ensName,
				Type = type,
			}, cancellationToken);

		_ = globalContext.Services.GlobalIndexService.UpdateEntryAsync(
			candidateAddress.RemoveHexPrefix(), address, EventIndex.Zero,
			entry => entry.AddressEnsName = ensName,
			cancellationToken: cancellationToken);
	}

	public static async Task<ReverseAddressLookupResult> VerifyAddressAsync(
		this GlobalContext globalContext, byte[] addressReverseNameHash, HexBigInteger blockNumber)
	{
		EnsReverseResult reverseResolutionResult =
			await globalContext.TryReverseResolutionAsync(addressReverseNameHash, blockNumber);

		if (reverseResolutionResult.ResolvedEnsNameHash is null)
		{
			return new ReverseAddressLookupResult
			{
				AddressReverseNameHash = reverseResolutionResult.ReverseNameHash,
				ReverseResolver = reverseResolutionResult.ReverseResolver,
				ReverseResolvedEnsName = reverseResolutionResult.ResolvedEnsName,
				ReverseResolvedEnsNameHash = reverseResolutionResult.ResolvedEnsNameHash,
				ForwardResolver = null,
				ForwardResolvedAddressReverseNameHash = null,
			};
		}

		ForwardResolutionResult forwardResolutionResult = await globalContext.TryForwardResolutionAsync(
			reverseResolutionResult.ResolvedEnsNameHash ??
			throw new InvalidOperationException("ResolvedEnsNameHash must not be empty"),
			blockNumber);

		return new ReverseAddressLookupResult
		{
			AddressReverseNameHash = reverseResolutionResult.ReverseNameHash,
			ReverseResolver = reverseResolutionResult.ReverseResolver,
			ReverseResolvedEnsName = reverseResolutionResult.ResolvedEnsName,
			ReverseResolvedEnsNameHash = reverseResolutionResult.ResolvedEnsNameHash,
			ForwardResolver = forwardResolutionResult.ForwardResolver,
			ForwardResolvedAddressReverseNameHash = forwardResolutionResult.ResolvedAddressReverseNameHash,
		};
	}

	public static async Task<EnsNameLookupResult> VerifyEnsNameAsync(
		this GlobalContext globalContext, byte[] ensNameHash, HexBigInteger blockNumber)
	{
		ForwardResolutionResult forwardResolutionResult =
			await globalContext.TryForwardResolutionAsync(ensNameHash, blockNumber);

		if (forwardResolutionResult.ResolvedAddressReverseNameHash is null)
		{
			return new EnsNameLookupResult
			{
				EnsNameHash = forwardResolutionResult.EnsNameHash,
				ForwardResolver = forwardResolutionResult.ForwardResolver,
				ForwardResolvedAddress = forwardResolutionResult.ResolvedAddress,
				ForwardResolvedAddressReverseNameHash = forwardResolutionResult.ResolvedAddressReverseNameHash,
				ReverseResolver = null,
				ReverseResolvedEnsName = null,
				ReverseResolvedEnsNameHash = null,
			};
		}

		EnsReverseResult reverseResolutionResult = await globalContext.TryReverseResolutionAsync(
			forwardResolutionResult.ResolvedAddressReverseNameHash ??
			throw new InvalidOperationException("ResolvedAddress must not be empty"), blockNumber);

		if ((long)blockNumber.Value == 23456942)
		{
		}

		return new EnsNameLookupResult
		{
			EnsNameHash = forwardResolutionResult.EnsNameHash,
			ForwardResolver = forwardResolutionResult.ForwardResolver,
			ForwardResolvedAddress = forwardResolutionResult.ResolvedAddress,
			ForwardResolvedAddressReverseNameHash = forwardResolutionResult.ResolvedAddressReverseNameHash,
			ReverseResolver = reverseResolutionResult.ReverseResolver,
			ReverseResolvedEnsName = reverseResolutionResult.ResolvedEnsName,
			ReverseResolvedEnsNameHash = reverseResolutionResult.ResolvedEnsNameHash,
		};
	}

	private static async Task<bool> UpdateNodeAsync(
		NodesContext nodesContext, Storage storage, string address, string? ensName,
		CancellationToken cancellationToken = default)
	{
		if (!nodesContext.Nodes.Data.Index.TryGetValue(address, out NodeIndexEntry? nodeIndexEntry))
		{
			return false;
		}

		nodesContext.Nodes.Data.Index[address] = nodeIndexEntry with
		{
			ContractAddressEnsName = ensName,
		};

		if (!nodesContext.Nodes.Partial.Updated.ContainsKey(address))
		{
			nodesContext.Nodes.Partial.Updated[address] =
				(await storage.ReadAsync<Node>(Keys.Node(address), cancellationToken))?.Data ??
				throw new InvalidOperationException("Cannot read node operator from storage.");
		}

		// TODO: Use list
		nodesContext.Nodes.Partial.Updated[address] =
			nodesContext.Nodes.Partial.Updated[address] with
			{
				ContractAddressEns = ensName,
			};

		foreach ((var key, MinipoolValidatorIndexEntry value) in nodesContext.ValidatorInfo.Data.MinipoolValidatorIndex)
		{
			nodesContext.ValidatorInfo.Data.MinipoolValidatorIndex[key] = value with
			{
				NodeAddressEnsName = ensName,
			};
		}

		foreach ((var key, MegapoolValidatorIndexEntry value) in nodesContext.ValidatorInfo.Data.MegapoolValidatorIndex)
		{
			nodesContext.ValidatorInfo.Data.MegapoolValidatorIndex[key] = value with
			{
				NodeAddressEnsName = ensName,
			};
		}

		nodesContext.QueueInfo.MinipoolFullQueue.ReplaceAll(
			x => x.NodeAddress.SequenceEqual(address.HexToByteArray()), entry => entry with
			{
				NodeAddressEns = ensName,
			});

		nodesContext.QueueInfo.MinipoolHalfQueue.ReplaceAll(
			x => x.NodeAddress.SequenceEqual(address.HexToByteArray()), entry => entry with
			{
				NodeAddressEns = ensName,
			});

		nodesContext.QueueInfo.MinipoolVariableQueue.ReplaceAll(
			x => x.NodeAddress.SequenceEqual(address.HexToByteArray()), entry => entry with
			{
				NodeAddressEns = ensName,
			});

		nodesContext.QueueInfo.MegapoolStandardQueue.ReplaceAll(
			x => x.NodeAddress.SequenceEqual(address.HexToByteArray()), entry => entry with
			{
				NodeAddressEns = ensName,
			});

		nodesContext.QueueInfo.MegapoolExpressQueue.ReplaceAll(
			x => x.NodeAddress.SequenceEqual(address.HexToByteArray()), entry => entry with
			{
				NodeAddressEns = ensName,
			});

		return true;
	}

	private static bool UpdateTokenInfo(TokenInfo tokenInfo, string address, string? ensName)
	{
		if (tokenInfo.Holders.TryGetValue(address, out HolderEntry? tokenIndexEntry))
		{
			tokenInfo.Holders[address] = tokenIndexEntry with
			{
				AddressEnsName = ensName,
			};

			return true;
		}

		return false;
	}
}