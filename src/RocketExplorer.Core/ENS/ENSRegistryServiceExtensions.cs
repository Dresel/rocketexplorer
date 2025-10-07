using Microsoft.Extensions.Logging;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Shared;

namespace RocketExplorer.Core.ENS;

public static class ENSRegistryServiceExtensions
{
	public static async Task<ForwardResolutionResult> TryForwardResolutionAsync(
		this ENSSyncContext context, byte[] ensNameHash, HexBigInteger blockNumber)
	{
		EnsUtil util = new();
		string? forwardResolverAddress;

		try
		{
			forwardResolverAddress = await context.Web3.Eth.GetEnsService().ENSRegistryService
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

		PublicResolverService forwardResolver = new(context.Web3.Eth, forwardResolverAddress);

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

	public static void TryRemoveFromEnsNameHash(this ENSSyncContext context, byte[] ensNameHash)
	{
		if (context.AddressToEnsNameHash.Contains(ensNameHash))
		{
			string addressToRemove = context.AddressToEnsNameHash[ensNameHash];
			context.Logger.LogWarning(
				"Remove entries from ens name {ens} => {address}", addressToRemove,
				context.AddressToReverseAddressNameHash[addressToRemove]);

			context.AddressToReverseAddressNameHash.Remove(addressToRemove);
			context.AddressToEnsNameHash.Remove(ensNameHash);
			context.EnsNameToEnsNameHash.Remove(ensNameHash);
		}
	}

	public static void TryRemoveFromReverseAddressNameHash(this ENSSyncContext context, byte[] reverseAddressNameHash)
	{
		if (context.AddressToReverseAddressNameHash.Contains(reverseAddressNameHash))
		{
			string addressToRemove = context.AddressToReverseAddressNameHash[reverseAddressNameHash];
			byte[] ensNameHash = context.AddressToEnsNameHash[addressToRemove];
			context.Logger.LogWarning(
				"Remove entries from reverse record {address} => {ens}", addressToRemove,
				context.EnsNameToEnsNameHash[ensNameHash]);

			context.AddressToEnsNameHash.Remove(addressToRemove);
			context.AddressToReverseAddressNameHash.Remove(reverseAddressNameHash);
			context.EnsNameToEnsNameHash.Remove(ensNameHash);
		}
	}

	public static async Task<EnsReverseResult> TryReverseResolutionAsync(
		this ENSSyncContext context, byte[] reverseAddressNameHash, HexBigInteger blockNumber)
	{
		EnsUtil ensUtil = new();

		string? reverseResolverAddress = await context.Web3.Eth.GetEnsService().ENSRegistryService
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

		PublicResolverService reverseResolver = new(context.Web3.Eth, reverseResolverAddress);

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
			context.Logger.LogDebug("Invalid ens name");
		}

		return new EnsReverseResult
		{
			ReverseNameHash = reverseAddressNameHash,
			ReverseResolver = reverseResolver,
			ResolvedEnsName = ensName,
			ResolvedEnsNameHash = ensNameHash,
		};
	}

	public static async Task<ReverseAddressLookupResult> VerifyAddress(
		this ENSSyncContext context, byte[] addressReverseNameHash, HexBigInteger blockNumber)
	{
		EnsReverseResult reverseResolutionResult = await context.TryReverseResolutionAsync(
			addressReverseNameHash, blockNumber);

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

		ForwardResolutionResult forwardResolutionResult = await context.TryForwardResolutionAsync(
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

	public static async Task<EnsNameLookupResult> VerifyEnsName(
		this ENSSyncContext context, byte[] ensNameHash, HexBigInteger blockNumber)
	{
		ForwardResolutionResult forwardResolutionResult =
			await context.TryForwardResolutionAsync(ensNameHash, blockNumber);

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

		EnsReverseResult reverseResolutionResult = await context.TryReverseResolutionAsync(
			forwardResolutionResult.ResolvedAddressReverseNameHash ??
			throw new InvalidOperationException("ResolvedAddress must not be empty"), blockNumber);

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
}