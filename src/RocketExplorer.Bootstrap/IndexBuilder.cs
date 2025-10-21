using System.Globalization;
using Nethereum.Hex.HexConvertors.Extensions;
using RocketExplorer.Core;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Tokens;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Bootstrap;

internal class IndexBuilder
{
	public static async Task BuildInitialIndexAsync(
		Storage storage, GlobalIndexService index, CancellationToken cancellationToken = default)
	{
		index.SkipLoading = true;

		BlobObject<NodesSnapshot>? nodesSnapshot =
			await storage.ReadAsync<NodesSnapshot>(Keys.NodesSnapshot, cancellationToken) ??
			throw new InvalidOperationException($"Snapshot {Keys.NodesSnapshot} not found");

		foreach (NodeIndexEntry nodeIndexEntry in nodesSnapshot.Data.Index)
		{
			_ = index.AddOrUpdateEntryAsync(
				nodeIndexEntry.ContractAddress.ToHex(), nodeIndexEntry.ContractAddress, EventIndex.Zero,
				x =>
				{
					x.Type |= IndexEntryType.NodeOperator;
					x.Address = nodeIndexEntry.ContractAddress;
				}, cancellationToken: cancellationToken);

			if (nodeIndexEntry.MegapoolAddress is not null)
			{
				_ = index.AddOrUpdateEntryAsync(
					nodeIndexEntry.MegapoolAddress.ToHex(),
					nodeIndexEntry.MegapoolAddress,
					EventIndex.Zero,
					x =>
					{
						x.Type |= IndexEntryType.Megapool;
						x.Address = nodeIndexEntry.ContractAddress;
						x.MegapoolAddress = nodeIndexEntry.MegapoolAddress;
					}, cancellationToken: cancellationToken);
			}
		}

		BlobObject<ValidatorSnapshot>? validatorSnapshot =
			await storage.ReadAsync<ValidatorSnapshot>(Keys.ValidatorSnapshot, cancellationToken) ??
			throw new InvalidOperationException($"Snapshot {Keys.ValidatorSnapshot} not found");

		foreach (MinipoolValidatorIndexEntry minipoolValidator in validatorSnapshot.Data
					.MinipoolValidatorIndex)
		{
			byte[] minipoolAddress = minipoolValidator.MinipoolAddress;

			_ = index.AddOrUpdateEntryAsync(
				minipoolAddress.ToHex(), minipoolAddress, EventIndex.Zero,
				x =>
				{
					x.Type |= IndexEntryType.MinipoolValidator;
					x.Address = minipoolAddress;
				}, cancellationToken: cancellationToken);

			if (minipoolValidator.PubKey is not null)
			{
				_ = index.AddOrUpdateEntryAsync(
					minipoolValidator.PubKey.ToHex(), minipoolAddress, EventIndex.Zero,
					x =>
					{
						x.Type |= IndexEntryType.MinipoolValidator;
						x.Address = minipoolAddress;
						x.ValidatorPubKey = minipoolValidator.PubKey;
					}, cancellationToken: cancellationToken);
			}

			if (minipoolValidator.ValidatorIndex is not null)
			{
				_ = index.AddOrUpdateEntryAsync(
					minipoolValidator.ValidatorIndex.Value.ToString(CultureInfo.InvariantCulture),
					minipoolAddress,
					EventIndex.Zero,
					x =>
					{
						x.Type |= IndexEntryType.MinipoolValidator;
						x.Address = minipoolAddress;
						x.ValidatorIndex = minipoolValidator.ValidatorIndex;
					}, cancellationToken: cancellationToken);
			}
		}

		foreach (MegapoolValidatorIndexEntry megapoolValidator in validatorSnapshot.Data
					.MegapoolValidatorIndex)
		{
			byte[] megapoolAddress = megapoolValidator.MegapoolAddress;

			int megapoolIndex = megapoolValidator.MegapoolIndex;

			_ = index.AddOrUpdateEntryAsync(
				megapoolAddress.ToHex(),
				megapoolAddress,
				EventIndex.Zero,
				x => { x.Type |= IndexEntryType.Megapool; }, cancellationToken: cancellationToken);

			_ = index.AddOrUpdateEntryAsync(
				megapoolValidator.PubKey.ToHex(),
				megapoolAddress.Concat(BitConverter.GetBytes(megapoolIndex)).ToArray(),
				EventIndex.Zero,
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
				_ = index.AddOrUpdateEntryAsync(
					megapoolValidator.ValidatorIndex.Value.ToString(CultureInfo.InvariantCulture), megapoolAddress
						.Concat(BitConverter.GetBytes(megapoolIndex))
						.ToArray(), EventIndex.Zero,
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

		BlobObject<TokensRETHSnapshot>? tokensRETHSnapshot =
			await storage.ReadAsync<TokensRETHSnapshot>(Keys.TokensRETHSnapshot, cancellationToken) ??
			throw new InvalidOperationException($"Snapshot {Keys.TokensRETHSnapshot} not found");

		foreach (HolderEntry holderEntry in tokensRETHSnapshot.Data.RETH.Holders)
		{
			_ = AddHolderAsync(
				index, holderEntry.Address.ToLower().HexToByteArray(), TokenType.RETH, cancellationToken);
		}

		BlobObject<TokensRockRETHSnapshot>? tokensRockRETHSnapshot =
			await storage.ReadAsync<TokensRockRETHSnapshot>(Keys.TokensRockRETHSnapshot, cancellationToken) ??
			throw new InvalidOperationException($"Snapshot {Keys.TokensRockRETHSnapshot} not found");

		foreach (HolderEntry holderEntry in tokensRockRETHSnapshot.Data.RockRETH?.Holders ?? [])
		{
			_ = AddHolderAsync(
				index, holderEntry.Address.ToLower().HexToByteArray(), TokenType.RockRETH, cancellationToken);
		}

		BlobObject<TokensRPLSnapshot>? tokensRPLSnapshot =
			await storage.ReadAsync<TokensRPLSnapshot>(Keys.TokensRPLSnapshot, cancellationToken) ??
			throw new InvalidOperationException($"Snapshot {Keys.TokensRPLSnapshot} not found");

		foreach (HolderEntry holderEntry in tokensRPLSnapshot.Data.RPL.Holders)
		{
			_ = AddHolderAsync(
				index, holderEntry.Address.ToLower().HexToByteArray(), TokenType.RPL, cancellationToken);
		}

		BlobObject<TokensRPLOldSnapshot>? tokensRPLOldSnapshot =
			await storage.ReadAsync<TokensRPLOldSnapshot>(Keys.TokensRPLOldSnapshot, cancellationToken) ??
			throw new InvalidOperationException($"Snapshot {Keys.TokensRPLOldSnapshot} not found");

		foreach (HolderEntry holderEntry in tokensRPLOldSnapshot.Data.RPLOld.Holders)
		{
			_ = AddHolderAsync(
				index, holderEntry.Address.ToLower().HexToByteArray(), TokenType.RPLOld, cancellationToken);
		}

		await index.WaitForCompletion(cancellationToken);

		index.SkipLoading = false;
	}

	private static Task AddHolderAsync(
		GlobalIndexService index, byte[] address, TokenType tokenType, CancellationToken cancellationToken = default) =>
		index.AddOrUpdateEntryAsync(
			address.ToHex(), address, EventIndex.Zero,
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
}