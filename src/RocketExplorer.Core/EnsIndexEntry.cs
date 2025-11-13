using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class EnsIndexEntry
{
	public byte[] Address { get; set; } = null!;

	public string AddressEnsName { get; set; } = null!;

	public IndexEntryType Type { get; set; }

	public HashSet<byte[]> NodeAddresses { get; set; } = new(new FastByteArrayComparer());
}