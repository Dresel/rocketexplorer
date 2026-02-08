using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Hex.HexConvertors.Extensions;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Ens;

namespace RocketExplorer.Core.Ens;

public record class EnsContext
{
	private readonly ReaderWriterLockSlim readerWriterLock = new();

	public required long CurrentBlockHeight { get; set; }

	public ReadOnlyDictionary<byte[], string> OldAddressEnsMap { get; set; } =
		new(new Dictionary<byte[], string>(new FastByteArrayComparer()));

	protected ConcurrentBictionary<string, byte[]> AddressToEnsNameHash { get; } = new(
		StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	protected ConcurrentBictionary<string, byte[]> AddressToReverseAddressNameHash { get; } = new(
		StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	protected ConcurrentBictionary<string, byte[]> EnsNameToEnsNameHash { get; } = new(
		StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	protected ConcurrentBictionary<string, byte[]> IndexReverseAddressNameHashMap { get; } = new(
		StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	public static async Task<EnsContext> ReadAsync(
		Storage storage, Task<NodesContext> nodesContextFactory, Task<TokensContextRPL> tokensContextRPLFactory,
		Task<TokensContextRPLOld> tokensContextRPLOldFactory, Task<TokensContextRETH> tokensContextRETHFactory,
		Task<TokensContextRockRETH> tokensContextRockRETHFactory,
		AddressEnsProcessHistory addressEnsProcessHistory, ILogger<EnsContext> logger,
		CancellationToken cancellationToken = default)
	{
		NodesContext nodesContext = await nodesContextFactory;

		TokensContextRPL tokensContextRPL = await tokensContextRPLFactory;
		TokensContextRPLOld tokensContextRPLOld = await tokensContextRPLOldFactory;
		TokensContextRETH tokensContextRETH = await tokensContextRETHFactory;
		TokensContextRockRETH tokensContextRockRETH = await tokensContextRockRETHFactory;

		await Task.WhenAll(
			nodesContext.IsFinished, tokensContextRPL.IsFinished, tokensContextRPLOld.IsFinished,
			tokensContextRETH.IsFinished, tokensContextRockRETH.IsFinished);

		logger.LogInformation("Loading {snapshot}", Keys.EnsSnapshot);
		BlobObject<EnsSnapshot>? ensSnapshot =
			await storage.ReadAsync<EnsSnapshot>(Keys.EnsSnapshot, cancellationToken);

		EnsContext ensContext = new()
		{
			CurrentBlockHeight = ensSnapshot?.ProcessedBlockNumber ?? 0,
		};

		// Add nodes and known node ens names
		ensContext.AddToReverseAddressNameHashMap(nodesContext.Nodes.Data.Index.Select(x => x.Value.ContractAddress));
		ensContext.AddToReverseAddressNameHashMap(
			nodesContext.Nodes.Data.WithdrawalAddresses.Select(x => x.Value.HexToByteArray()));
		ensContext.AddToReverseAddressNameHashMap(
			nodesContext.Nodes.Data.RPLWithdrawalAddresses.Select(x => x.Value.HexToByteArray()));
		ensContext.AddToReverseAddressNameHashMap(
			nodesContext.Nodes.Data.StakeOnBehalfAddresses.SelectMany(list =>
				list.Value.Select(x => x.HexToByteArray())));

		ensContext.AddToReverseAddressNameHashMap(
			tokensContextRETH.RETHTokenInfo.Holders.Select(x => x.Value.Address.HexToByteArray()));
		ensContext.AddToReverseAddressNameHashMap(
			tokensContextRockRETH.RockRETHTokenInfo.Holders.Select(x => x.Value.Address.HexToByteArray()));
		ensContext.AddToReverseAddressNameHashMap(
			tokensContextRPL.RPLTokenInfo.Holders.Select(x => x.Value.Address.HexToByteArray()));
		ensContext.AddToReverseAddressNameHashMap(
			tokensContextRPLOld.RPLOldTokenInfo.Holders.Select(x => x.Value.Address.HexToByteArray()));

		ensContext.AddToEnsMaps(ensSnapshot?.Data.AddressEnsMap.Select(pair => (pair.Key, pair.Value)) ?? []);

		ensContext.OldAddressEnsMap =
			ensSnapshot?.Data.AddressEnsMap.ToDictionary(new FastByteArrayComparer()).AsReadOnly() ??
			new ReadOnlyDictionary<byte[], string>(new Dictionary<byte[], string>(new FastByteArrayComparer()));

		return ensContext;
	}

	public void AddToEnsMaps(IEnumerable<(byte[] Address, string EnsName)> addressEnsEntries)
	{
		EnsUtil ensUtil = new();

		this.readerWriterLock.EnterWriteLock();

		try
		{
			foreach ((byte[] address, string ensName) in addressEnsEntries)
			{
				string addressHex = address.ToHex(true);

				AddressToReverseAddressNameHash[addressHex] = ensUtil.ToReverseAddressNameHash(addressHex);
				AddressToEnsNameHash[addressHex] = ensUtil.GetNameHash(ensName).HexToByteArray();
				EnsNameToEnsNameHash[ensName] = ensUtil.GetNameHash(ensName).HexToByteArray();
			}
		}
		finally
		{
			this.readerWriterLock.ExitWriteLock();
		}
	}

	public void AddToReverseAddressNameHashMap(IEnumerable<byte[]> addresses)
	{
		EnsUtil ensUtil = new();

		foreach (byte[] address in addresses)
		{
			string addressHex = address.ToHex(true);
			IndexReverseAddressNameHashMap[addressHex] = ensUtil.ToReverseAddressNameHash(addressHex);
		}
	}

	public bool ContainsEnsForAddress(string address) =>
		AddressToEnsNameHash.Contains(address);

	public bool ContainsEnsName(string ensName) =>
		EnsNameToEnsNameHash.Contains(ensName);

	public bool ContainsReverseAddressNameHash(byte[] addressReverseNameHash) =>
		IndexReverseAddressNameHashMap.Contains(addressReverseNameHash);

	public bool IsKnownAddress(string address) =>
		IndexReverseAddressNameHashMap.Contains(address);

	public async Task SaveAsync(
		Storage storage, ILogger<EnsContext> logger, CancellationToken cancellationToken = default)
	{
		List<(byte[] Address, string EnsName)> addressEnsEntries = new();

		foreach ((string address, byte[] ensNameHash) in AddressToEnsNameHash)
		{
			string ensName = EnsNameToEnsNameHash[ensNameHash];

			addressEnsEntries.Add((address.HexToByteArray(), ensName));
		}

		logger.LogInformation("Writing {snapshot}", Keys.EnsSnapshot);
		await storage.WriteAsync(
			Keys.EnsSnapshot,
			new BlobObject<EnsSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new EnsSnapshot
				{
					AddressEnsMap = new Dictionary<byte[], string>(
						addressEnsEntries.Select(x => new KeyValuePair<byte[], string>(x.Address, x.EnsName))),
				},
			}, cancellationToken: cancellationToken);
	}

	public bool TryGetAddressFromReverseAddressNameHash(byte[] addressReverseNameHash, out string? value) =>
		IndexReverseAddressNameHashMap.TryGetValue(addressReverseNameHash, out value);

	public string? TryGetEnsNameFromAddress(string address)
	{
		this.readerWriterLock.EnterReadLock();

		try
		{
			if (AddressToEnsNameHash.TryGetValue(address, out byte[]? ensNameHash))
			{
				return EnsNameToEnsNameHash[ensNameHash!];
			}

			return null;
		}
		finally
		{
			this.readerWriterLock.ExitReadLock();
		}
	}

	public (string RemovedAddress, string RemovedEnsName)? TryRemoveFromEnsNameHash(byte[] ensNameHash)
	{
		this.readerWriterLock.EnterWriteLock();

		string addressToRemove;
		string ensNameToRemove;

		try
		{
			if (!AddressToEnsNameHash.Contains(ensNameHash))
			{
				return null;
			}

			addressToRemove = AddressToEnsNameHash[ensNameHash];
			ensNameToRemove = EnsNameToEnsNameHash[ensNameHash];

			AddressToReverseAddressNameHash.Remove(addressToRemove);
			AddressToEnsNameHash.Remove(ensNameHash);
			EnsNameToEnsNameHash.Remove(ensNameHash);
		}
		finally
		{
			this.readerWriterLock.ExitWriteLock();
		}

		return (addressToRemove, ensNameToRemove);
	}

	public (string RemovedAddress, string RemovedEns)? TryRemoveFromReverseAddressNameHash(
		byte[] reverseAddressNameHash)
	{
		this.readerWriterLock.EnterWriteLock();

		string addressToRemove;
		string ensNameToRemove;

		try
		{
			if (!AddressToReverseAddressNameHash.Contains(reverseAddressNameHash))
			{
				return null;
			}

			addressToRemove = AddressToReverseAddressNameHash[reverseAddressNameHash];
			byte[] ensNameHash = AddressToEnsNameHash[addressToRemove];
			ensNameToRemove = EnsNameToEnsNameHash[ensNameHash];

			AddressToEnsNameHash.Remove(addressToRemove);
			AddressToReverseAddressNameHash.Remove(reverseAddressNameHash);
			EnsNameToEnsNameHash.Remove(ensNameHash);
		}
		finally
		{
			this.readerWriterLock.ExitWriteLock();
		}

		return (addressToRemove, ensNameToRemove);
	}
}