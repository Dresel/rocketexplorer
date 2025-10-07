using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class IndexEntry
{
	public required byte[] Address { get; set; }

	public string? AddressEnsName { get; set; }

	public int? MegapoolIndex { get; set; }

	public IndexEntryType Type { get; set; }

	public long? ValidatorIndex { get; set; }

	public byte[]? ValidatorPubKey { get; set; }
}