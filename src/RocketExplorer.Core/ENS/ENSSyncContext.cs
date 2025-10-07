using Nethereum.Contracts.Standards.ENS;
using Nethereum.Hex.HexConvertors.Extensions;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Core.ENS;

public record class ENSSyncContext : ContextBase
{
	public EnsUtil EnsUtil { get; } = new();

	public Bictionary<string, byte[]> IndexReverseAddressNameHashMap { get; } = new(StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	public Bictionary<string, byte[]> EnsNameToEnsNameHash { get; } = new(StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	public Bictionary<string, byte[]> AddressToEnsNameHash { get; } = new(StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	public Bictionary<string, byte[]> AddressToReverseAddressNameHash { get; } = new(StringComparer.OrdinalIgnoreCase, new FastByteArrayComparer());

	public void AddToReverseAddressNameHashMap(IEnumerable<byte[]> addresses)
	{
		EnsUtil ensUtil = new();

		foreach (byte[] address in addresses)
		{
			string addressHex = address.ToHex(true);
			IndexReverseAddressNameHashMap[addressHex] = ensUtil.ToReverseAddressNameHash(addressHex);
		}
	}

	public void AddToEnsMaps(IEnumerable<(byte[] Address, string EnsName)> addressEnsEntries)
	{
		EnsUtil ensUtil = new();

		foreach ((byte[] address, string ensName) in addressEnsEntries)
		{
			string addressHex = address.ToHex(true);

			AddressToReverseAddressNameHash[addressHex] = ensUtil.ToReverseAddressNameHash(addressHex);
			AddressToEnsNameHash[addressHex] = ensUtil.GetNameHash(ensName).HexToByteArray();
			EnsNameToEnsNameHash[ensName] = ensUtil.GetNameHash(ensName).HexToByteArray();
		}
	}
}