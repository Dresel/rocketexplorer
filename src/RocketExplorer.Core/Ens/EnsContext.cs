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

	protected ConcurrentBictionary<string, byte[]> AddressToEnsNameHash { get; } = new(
		StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	protected ConcurrentBictionary<string, byte[]> AddressToReverseAddressNameHash { get; } = new(
		StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	protected ConcurrentBictionary<string, byte[]> EnsNameToEnsNameHash { get; } = new(
		StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	protected ConcurrentBictionary<string, byte[]> IndexReverseAddressNameHashMap { get; } = new(
		StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	public static async Task<EnsContext> ReadAsync(
		Storage storage, Task<NodesContext> nodesContextFactory, Task<TokensContext> tokensContextFactory,
		AddressEnsProcessHistory addressEnsProcessHistory, ILogger<EnsContext> logger,
		CancellationToken cancellationToken = default)
	{
		NodesContext nodesContext = await nodesContextFactory;
		TokensContext tokensContext = await tokensContextFactory;

		await Task.WhenAll(nodesContext.IsFinished, tokensContext.IsFinished);

		logger.LogInformation("Loading {snapshot}", Keys.EnsSnapshot);
		BlobObject<EnsSnapshot>? ensSnapshot =
			await storage.ReadAsync<EnsSnapshot>(Keys.EnsSnapshot, cancellationToken);

		EnsContext ensContext = new()
		{
			CurrentBlockHeight = ensSnapshot?.ProcessedBlockNumber ?? 0,
		};

		// Add nodes and known node ens names
		ensContext.AddToReverseAddressNameHashMap(nodesContext.Nodes.Data.Index.Select(x => x.Value.ContractAddress));
		ensContext.AddToEnsMaps(
			nodesContext.Nodes.Data.Index.Where(x => x.Value.ContractAddressEnsName is not null)
				.Select(x => (x.Value.ContractAddress, x.Value.ContractAddressEnsName!)));

		ensContext.AddToReverseAddressNameHashMap(
			tokensContext.RETHTokenInfo.Holders.Select(x => x.Value.Address.HexToByteArray()));
		ensContext.AddToEnsMaps(
			tokensContext.RETHTokenInfo.Holders.Where(x => x.Value.AddressEnsName is not null)
				.Select(x => (x.Value.Address.HexToByteArray(), x.Value.AddressEnsName!)));

		ensContext.AddToReverseAddressNameHashMap(
			tokensContext.RockRETHTokenInfo.Holders.Select(x => x.Value.Address.HexToByteArray()));
		ensContext.AddToEnsMaps(
			tokensContext.RockRETHTokenInfo.Holders.Where(x => x.Value.AddressEnsName is not null)
				.Select(x => (x.Value.Address.HexToByteArray(), x.Value.AddressEnsName!)));

		ensContext.AddToReverseAddressNameHashMap(
			tokensContext.RPLTokenInfo.Holders.Select(x => x.Value.Address.HexToByteArray()));
		ensContext.AddToEnsMaps(
			tokensContext.RPLTokenInfo.Holders.Where(x => x.Value.AddressEnsName is not null)
				.Select(x => (x.Value.Address.HexToByteArray(), x.Value.AddressEnsName!)));

		ensContext.AddToReverseAddressNameHashMap(
			tokensContext.RPLOldTokenInfo.Holders.Select(x => x.Value.Address.HexToByteArray()));
		ensContext.AddToEnsMaps(
			tokensContext.RPLOldTokenInfo.Holders.Where(x => x.Value.AddressEnsName is not null)
				.Select(x => (x.Value.Address.HexToByteArray(), x.Value.AddressEnsName!)));

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
		logger.LogInformation("Writing {snapshot}", Keys.EnsSnapshot);
		await storage.WriteAsync(
			Keys.EnsSnapshot,
			new BlobObject<EnsSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new EnsSnapshot(),
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