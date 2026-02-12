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
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Bootstrap;

internal class IndexBuilder
{
	public static async Task BuildIndexesAsync(GlobalContext globalContext)
	{
		globalContext.ContractsContext.ProcessingCompletionSource.TrySetResult();

		NodesMasterContext nodesContext = await globalContext.NodesMasterContextFactory;
		nodesContext.ProcessingCompletionSource.TrySetResult();

		(await globalContext.TokensContextRPLFactory).ProcessingCompletionSource.TrySetResult();
		(await globalContext.TokensContextRPLOldFactory).ProcessingCompletionSource.TrySetResult();
		(await globalContext.TokensContextStakedRPLFactory).ProcessingCompletionSource.TrySetResult();
		(await globalContext.TokensContextRETHFactory).ProcessingCompletionSource.TrySetResult();
		(await globalContext.TokensContextRockRETHFactory).ProcessingCompletionSource.TrySetResult();

		await BuildInitialIndexAsync(globalContext);
		await BuildInitialEnsIndexAsync(globalContext);

		await globalContext.Services.GlobalIndexService.WriteAsync(globalContext.ContractsContext.CurrentBlockHeight);
		await globalContext.Services.GlobalEnsIndexService.WriteAsync(
			globalContext.ContractsContext.CurrentBlockHeight);

		EnsContext ensContext = await globalContext.EnsContextFactory;

		ensContext.CurrentBlockHeight = nodesContext.CurrentBlockHeight;
		await ensContext.SaveAsync(globalContext.Services.Storage, globalContext.LoggerFactory.CreateLogger<EnsContext>());
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

		NodesMasterContext nodesContext = await globalContext.NodesMasterContextFactory;

		TokensContextRPL tokensContextRPL = await globalContext.TokensContextRPLFactory;
		TokensContextRPLOld tokensContextRPLOld = await globalContext.TokensContextRPLOldFactory;
		TokensContextRETH tokensContextRETH = await globalContext.TokensContextRETHFactory;
		TokensContextRockRETH tokensContextRockRETH = await globalContext.TokensContextRockRETHFactory;

		EnsContext ensContext = await globalContext.EnsContextFactory;

		globalContext.Services.GlobalEnsIndexService.SkipLoading = true;

		HashSet<byte[]> addresses = new(new FastByteArrayComparer());

		foreach (byte[] addressBytes in nodesContext.Nodes.Data.Nodes.Select(x => x.Value.ContractAddress))
		{
			addresses.Add(addressBytes);
		}

		foreach (HolderEntry holder in tokensContextRPL.RPLTokenInfo.Holders.Values)
		{
			addresses.Add(holder.Address.HexToByteArray());
		}

		foreach (HolderEntry holder in tokensContextRPLOld.RPLOldTokenInfo.Holders.Values)
		{
			addresses.Add(holder.Address.HexToByteArray());
		}

		foreach (HolderEntry holder in tokensContextRETH.RETHTokenInfo.Holders.Values)
		{
			addresses.Add(holder.Address.HexToByteArray());
		}

		foreach (HolderEntry holder in tokensContextRockRETH.RockRETHTokenInfo.Holders.Values)
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

		NodesMasterContext nodesContext = await globalContext.NodesMasterContextFactory;

		foreach (NodeMasterInfo nodeMaster in nodesContext.Nodes.Data.Nodes.Values)
		{
			_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
				nodeMaster.ContractAddress.ToHex(), nodeMaster.ContractAddress, EventIndex.Next,
				x =>
				{
					x.Type |= IndexEntryType.NodeOperator;
					x.Address = nodeMaster.ContractAddress;
				}, cancellationToken: cancellationToken);

			if (nodeMaster.MegapoolAddress is not null)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					nodeMaster.MegapoolAddress.ToHex(),
					nodeMaster.MegapoolAddress,
					EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.Megapool;
						x.Address = nodeMaster.ContractAddress;
						x.MegapoolAddress = nodeMaster.MegapoolAddress;
					}, cancellationToken: cancellationToken);
			}

			if (nodeMaster.WithdrawalAddress is not null)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					nodeMaster.WithdrawalAddress.ToHex(), nodeMaster.WithdrawalAddress,
					EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.WithdrawalAddress;
						x.Address = nodeMaster.WithdrawalAddress;
						x.NodeAddresses.Add(nodeMaster.ContractAddress);
					}, cancellationToken: cancellationToken);
			}

			if (nodeMaster.RPLWithdrawalAddress is not null)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					nodeMaster.RPLWithdrawalAddress.ToHex(), nodeMaster.RPLWithdrawalAddress,
					EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.RPLWithdrawalAddress;
						x.Address = nodeMaster.RPLWithdrawalAddress;
						x.NodeAddresses.Add(nodeMaster.ContractAddress);
					}, cancellationToken: cancellationToken);
			}

			foreach (byte[] stakeOnBehalfAddress in nodeMaster.StakeOnBehalfAddresses)
			{
				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					stakeOnBehalfAddress.ToHex(), stakeOnBehalfAddress,
					EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.StakeOnBehalfAddress;
						x.Address = stakeOnBehalfAddress;
						x.NodeAddresses.Add(nodeMaster.ContractAddress);
					}, cancellationToken: cancellationToken);
			}

			foreach (ValidatorMasterInfo minipoolValidator in nodeMaster.MinipoolValidators.Values)
			{
				byte[] minipoolAddress = minipoolValidator.MinipoolAddress!;

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

			foreach (ValidatorMasterInfo megapoolValidator in nodeMaster.MegapoolValidators.Values)
			{
				byte[] megapoolAddress = megapoolValidator.MegapoolAddress!;
				int megapoolIndex = megapoolValidator.MegapoolIndex!.Value;

				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					megapoolAddress.ToHex(),
					megapoolAddress,
					EventIndex.Next,
					x => { x.Type |= IndexEntryType.Megapool; }, cancellationToken: cancellationToken);

				_ = globalContext.Services.GlobalIndexService.AddOrUpdateEntryAsync(
					megapoolValidator.PubKey!.ToHex(),
					megapoolAddress.Concat(BitConverter.GetBytes(megapoolIndex)).ToArray(),
					EventIndex.Next,
					x =>
					{
						x.Type |= IndexEntryType.MegapoolValidator;
						x.Address = nodeMaster.ContractAddress;
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
							x.Address = nodeMaster.ContractAddress;
							x.MegapoolAddress = megapoolAddress;
							x.ValidatorIndex = megapoolValidator.ValidatorIndex;
							x.MegapoolIndex = megapoolIndex;
						}, cancellationToken: cancellationToken);
				}
			}
		}

		TokensContextRPL tokensContextRPL = await globalContext.TokensContextRPLFactory;
		TokensContextRPLOld tokensContextRPLOld = await globalContext.TokensContextRPLOldFactory;
		TokensContextRETH tokensContextRETH = await globalContext.TokensContextRETHFactory;
		TokensContextRockRETH tokensContextRockRETH = await globalContext.TokensContextRockRETHFactory;

		foreach (HolderEntry holderEntry in tokensContextRETH.RETHTokenInfo.Holders.Values)
		{
			_ = AddHolderAsync(
				globalContext.Services.GlobalIndexService, holderEntry.Address.HexToByteArray(),
				TokenType.RETH, cancellationToken);
		}

		foreach (HolderEntry holderEntry in tokensContextRockRETH.RockRETHTokenInfo.Holders.Values)
		{
			_ = AddHolderAsync(
				globalContext.Services.GlobalIndexService, holderEntry.Address.HexToByteArray(),
				TokenType.RockRETH, cancellationToken);
		}

		foreach (HolderEntry holderEntry in tokensContextRPL.RPLTokenInfo.Holders.Values)
		{
			_ = AddHolderAsync(
				globalContext.Services.GlobalIndexService, holderEntry.Address.HexToByteArray(),
				TokenType.RPL, cancellationToken);
		}

		foreach (HolderEntry holderEntry in tokensContextRPLOld.RPLOldTokenInfo.Holders.Values)
		{
			_ = AddHolderAsync(
				globalContext.Services.GlobalIndexService, holderEntry.Address.HexToByteArray(),
				TokenType.RPLOld, cancellationToken);
		}
	}
}