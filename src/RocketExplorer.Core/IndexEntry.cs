using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class IndexEntry
{
	public byte[] Address { get; set; } = null!;

	public string? AddressEnsName { get; set; }

	public byte[]? MegapoolAddress { get; set; }

	public int? MegapoolIndex { get; set; }

	public IndexEntryType Type { get; set; }

	public long? ValidatorIndex { get; set; }

	public byte[]? ValidatorPubKey { get; set; }

	public List<byte[]> NodeAddresses { get; set; } = new();
}