using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using RocketExplorer.Core;
using RocketExplorer.Core.Ens;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Tokens;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Bootstrap;

internal class IndexBuilder
{
	public static async Task BuildIndexesAsync(GlobalContext globalContext)
	{
		globalContext.ContractsContext.ProcessingCompletionSource.TrySetResult();

		NodesContext nodesContext = await globalContext.NodesContextFactory;
		nodesContext.ProcessingCompletionSource.TrySetResult();

		TokensContext tokensContext = await globalContext.TokensContextFactory;
		tokensContext.ProcessingCompletionSource.TrySetResult();

		await BuildInitialIndexAsync(globalContext);
		await BuildInitialEnsIndexAsync(globalContext);

		await globalContext.Services.GlobalIndexService.WriteAsync(globalContext.ContractsContext.CurrentBlockHeight);
		await globalContext.Services.GlobalEnsIndexService.WriteAsync(
			globalContext.ContractsContext.CurrentBlockHeight);

		Task writeNodesTask = nodesContext.SaveAsync(
			globalContext.Services.Storage, globalContext.LoggerFactory.CreateLogger<NodesContext>());
		Task writeTokensTask = tokensContext.SaveAsync(
			globalContext.Services.Storage, globalContext.LoggerFactory.CreateLogger<TokensContext>());
		EnsContext ensContext = await globalContext.EnsContextFactory;

		ensContext.CurrentBlockHeight = nodesContext.CurrentBlockHeight;
		Task writeEnsTask = ensContext.SaveAsync(
			globalContext.Services.Storage, globalContext.LoggerFactory.CreateLogger<EnsContext>());

		await Task.WhenAll(writeNodesTask, writeTokensTask, writeEnsTask);
	}

	private static Task AddHolderAsync(
		GlobalIndexService index, byte[] address, TokenType tokenType, CancellationToken cancellationToken = default) =>
		index.AddOrUpdateEntryAsync(
			address.ToHex(), address, EventIndex.Next,
			entry =>
			{
				entry.Type |= tokenType switch
				{
					TokenType.RPL => IndexEntryType.RPLHolder,
					TokenType.RPLOld => IndexEntryType.RPLOldHolder,
					TokenType.RETH => IndexEntryType.RETHHolder,
					TokenType.RockRETH => IndexEntryType.RockRETHHolder,
					_ => throw new InvalidOperationException("Unknown token type"),
				};
				entry.Address = address;
			}, cancellationToken: cancellationToken);

	private static async Task BuildInitialEnsIndexAsync(
		GlobalContext globalContext, CancellationToken cancellationToken = default)
	{
		globalContext.GetLogger<EnsContext>().LogInformation("Building initial ens index");

		NodesContext nodesContext = await globalContext.NodesContextFactory;
		TokensContext tokensContext = await globalContext.TokensContextFactory;
		EnsContext ensContext = await globalContext.EnsContextFactory;

		globalContext.Services.GlobalEnsIndexService.SkipLoading = true;

		HashSet<byte[]> addresses = new(new FastByteArrayComparer());

		foreach (byte[] addressBytes in nodesContext.Nodes.Data.Index.Select(x => x.Value.ContractAddress))
		{
			addresses.Add(addressBytes);
		}

		foreach (HolderEntry holder in tokensContext.RPLTokenInfo.Holders.Values)
		{
			addresses.Add(holder.Address.HexToByteArray());
		}

		foreach (HolderEntry holder in tokensContext.RPLOldTokenInfo.Holders.Values)
		{
			addresses.Add(holder.Address.HexToByteArray());
		}

		foreach (HolderEntry holder in tokensContext.RETHTokenInfo.Holders.Values)
		{
			addresses.Add(holder.Address.HexToByteArray());
		}

		foreach (HolderEntry holder in tokensContext.RockRETHTokenInfo.Holders.Values)
		{
			addresses.Add(holder.Address.HexToByteArray());
		}

		EnsUtil ensUtil = new();

		ConcurrentBag<(byte[] Address, string Ens)> resolvedAddresses = new();

		int count = 0;

		await Parallel.ForEachAsync(
			addresses, cancellationToken, async (address, innerCancellationToken) =>
			{
				int localCount = Interlocked.Increment(ref count);

				if (count % 1000 == 0)
				{
					globalContext.LoggerFactory.CreateLogger<IndexBuilder>()
						.LogInformation("{Count} addresses resolved", localCount);
				}

				byte[] reverseAddressNameHash = ensUtil.ToReverseAddressNameHash(address.ToHex(true));
				ReverseAddressLookupResult reverseAddressLookupResult = await globalContext.VerifyAddressAsync(
					reverseAddressNameHash, new HexBigInteger(globalContext.LatestBlockHeight));

				if (!reverseAddressLookupResult.IsValidPrimary)
				{
					// Resolution failed
					return;
				}

				resolvedAddresses.Add((address, reverseAddressLookupResult.ReverseResolvedEnsName!));
			});

		count = 0;

		ensContext.AddToEnsMaps(resolvedAddresses);

		foreach ((byte[] address, string ens) in resolvedAddresses)
		{
			if (++count % 1000 == 0)
			{
				globalContext.LoggerFactory.CreateLogger<IndexBuilder>()
					.LogInformation("{Count} ens names updated", count);
			}

			await globalContext.UpdateEnsNameAsync(null, address, ens, cancellationToken);
		}
	}

	private static async Task BuildInitialIndexAsync(
		GlobalContext globalContext, CancellationToken cancellationToken = default)
	{
		globalContext.GetLogger<EnsContext>().LogInformation("Building initial index");

		globalContext.Services.GlobalIndexService.SkipLoading = true;

		NodesContext nodesContext = await globalContext.NodesContextFactory;

		foreach (NodeIndexEntry nodeIndexEntry in nodesContext.Nodes.Data.Index.Values)
		{
			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				nodeIndexEntry.ContractAddress.ToHex(), nodeIndexEntry.ContractAddress, EventIndex.Next,
				x =>
				{
					x.Type |= IndexEntryType.NodeOperator;
					x.Address = nodeIndexEntry.ContractAddress;
				}, cancellationToken: cancellationToken);

			if (nodeIndexEntry.MegapoolAddress is not null)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					nodeIndexEntry.MegapoolAddress.ToHex(),
					nodeIndexEntry.MegapoolAddress,
					EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.Megapool;
						x.Address = nodeIndexEntry.ContractAddress;
						x.MegapoolAddress = nodeIndexEntry.MegapoolAddress;
					}, cancellationToken: cancellationToken);
			}
		}

		foreach (KeyValuePair<string, string> withdrawalAddress in nodesContext.Nodes.Data.WithdrawalAddresses)
		{
			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				withdrawalAddress.Value.RemoveHexPrefix(), withdrawalAddress.Value.HexToByteArray(),
				EventIndex.Next,
				x =>
				{
					x.Type |= IndexEntryType.WithdrawalAddress;
					x.Address = withdrawalAddress.Value.HexToByteArray();
					x.NodeAddresses.Add(withdrawalAddress.Key.HexToByteArray());
				}, cancellationToken: cancellationToken);
		}

		foreach (KeyValuePair<string, string> withdrawalAddress in nodesContext.Nodes.Data.RPLWithdrawalAddresses)
		{
			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				withdrawalAddress.Value.RemoveHexPrefix(), withdrawalAddress.Value.HexToByteArray(),
				EventIndex.Next,
				x =>
				{
					x.Type |= IndexEntryType.RPLWithdrawalAddress;
					x.Address = withdrawalAddress.Value.HexToByteArray();
					x.NodeAddresses.Add(withdrawalAddress.Key.HexToByteArray());
				}, cancellationToken: cancellationToken);
		}

		foreach (var stakeOnBehalfAddresses in nodesContext.Nodes.Data.StakeOnBehalfAddresses)
		{
			foreach (string stakeOnBehalfAddress in stakeOnBehalfAddresses.Value)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					stakeOnBehalfAddress.RemoveHexPrefix(), stakeOnBehalfAddress.HexToByteArray(),
					EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.StakeOnBehalfAddress;
						x.Address = stakeOnBehalfAddress.HexToByteArray();
						x.NodeAddresses.Add(stakeOnBehalfAddresses.Key.HexToByteArray());
					}, cancellationToken: cancellationToken);
			}
		}

		foreach (MinipoolValidatorIndexEntry minipoolValidator in nodesContext.ValidatorInfo.Data
					.MinipoolValidatorIndex.Values)
		{
			byte[] minipoolAddress = minipoolValidator.MinipoolAddress;

			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				minipoolAddress.ToHex(), minipoolAddress, EventIndex.Next,
				x =>
				{
					x.Type |= IndexEntryType.MinipoolValidator;
					x.Address = minipoolAddress;
				}, cancellationToken: cancellationToken);

			if (minipoolValidator.PubKey is not null)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					minipoolValidator.PubKey.ToHex(), minipoolAddress, EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.MinipoolValidator;
						x.Address = minipoolAddress;
						x.ValidatorPubKey = minipoolValidator.PubKey;
					}, cancellationToken: cancellationToken);
			}

			if (minipoolValidator.ValidatorIndex is not null)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					minipoolValidator.ValidatorIndex.Value.ToString(CultureInfo.InvariantCulture),
					minipoolAddress,
					EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.MinipoolValidator;
						x.Address = minipoolAddress;
						x.ValidatorIndex = minipoolValidator.ValidatorIndex;
					}, cancellationToken: cancellationToken);
			}
		}

		foreach (MegapoolValidatorIndexEntry megapoolValidator in nodesContext.ValidatorInfo.Data
					.MegapoolValidatorIndex.Values)
		{
			byte[] megapoolAddress = megapoolValidator.MegapoolAddress;

			int megapoolIndex = megapoolValidator.MegapoolIndex;

			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				megapoolAddress.ToHex(),
				megapoolAddress,
				EventIndex.Next,
				x => { x.Type |= IndexEntryType.Megapool; }, cancellationToken: cancellationToken);

			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				megapoolValidator.PubKey.ToHex(),
				megapoolAddress.Concat(BitConverter.GetBytes(megapoolIndex)).ToArray(),
				EventIndex.Next,
				x =>
				{
					x.Type |= IndexEntryType.MegapoolValidator;
					x.Address = megapoolValidator.NodeAddress; // TODO: Check
					x.MegapoolAddress = megapoolAddress;
					x.ValidatorPubKey = megapoolValidator.PubKey;
					x.MegapoolIndex = megapoolIndex;
				}, cancellationToken: cancellationToken);

			if (megapoolValidator.ValidatorIndex is not null)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					megapoolValidator.ValidatorIndex.Value.ToString(CultureInfo.InvariantCulture), megapoolAddress
						.Concat(BitConverter.GetBytes(megapoolIndex))
						.ToArray(), EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.MegapoolValidator;
						x.Address = megapoolValidator.NodeAddress; // TODO: Check
						x.MegapoolAddress = megapoolAddress;
						x.ValidatorIndex = megapoolValidator.ValidatorIndex;
						x.MegapoolIndex = megapoolIndex;
					}, cancellationToken: cancellationToken);
			}
		}

		TokensContext tokensContext = await globalContext.TokensContextFactory;

		foreach (HolderEntry holderEntry in tokensContext.RETHTokenInfo.Holders.Values)
		{
			_ = AddHolderAsync(
				globalContext.Services.GlobalIndexService, holderEntry.Address.HexToByteArray(),
				TokenType.RETH, cancellationToken);
		}

		foreach (HolderEntry holderEntry in tokensContext.RockRETHTokenInfo.Holders.Values)
		{
			_ = AddHolderAsync(
				globalContext.Services.GlobalIndexService, holderEntry.Address.HexToByteArray(),
				TokenType.RockRETH, cancellationToken);
		}

		foreach (HolderEntry holderEntry in tokensContext.RPLTokenInfo.Holders.Values)
		{
			_ = AddHolderAsync(
				globalContext.Services.GlobalIndexService, holderEntry.Address.HexToByteArray(),
				TokenType.RPL, cancellationToken);
		}

		foreach (HolderEntry holderEntry in tokensContext.RPLOldTokenInfo.Holders.Values)
		{
			_ = AddHolderAsync(
				globalContext.Services.GlobalIndexService, holderEntry.Address.HexToByteArray(),
				TokenType.RPLOld, cancellationToken);
		}
	}
}