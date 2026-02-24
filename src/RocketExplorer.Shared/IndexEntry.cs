using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class IndexEntry : IIdentifiable<byte[]>
{
	[Key(0)]
	public required byte[] Identifier { get; init; }

	[Key(1)]
	public required IndexEntryType Type { get; init; }

	[Key(2)]
	public required byte[] Address { get; init; }

	[Key(3)]
	public required string? AddressEnsName { get; init; }

	[Key(4)]
	public required byte[]? MegapoolAddress { get; init; }

	[Key(5)]
	public required byte[]? ValidatorPubKey { get; init; }

	[Key(6)]
	public required long? ValidatorIndex { get; init; }

	[Key(7)]
	public required int? MegapoolIndex { get; init; }

	[Key(8)]
	public required List<byte[]> NodeAddresses { get; init; }

	public virtual bool Equals(IndexEntry? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return Identifier.SequenceEqual(other.Identifier) &&
			Type == other.Type &&
			Address.SequenceEqual(other.Address) &&
			((MegapoolAddress is null && other.MegapoolAddress is null) ||
				MegapoolAddress?.SequenceEqual(other.MegapoolAddress) == true) &&
			((ValidatorPubKey is null && other.ValidatorPubKey is null) ||
				ValidatorPubKey?.SequenceEqual(other.ValidatorPubKey) == true) &&
			ValidatorIndex == other.ValidatorIndex && MegapoolIndex == other.MegapoolIndex &&
			string.Equals(AddressEnsName, other.AddressEnsName, StringComparison.OrdinalIgnoreCase) &&
			NodeAddresses.SequenceEqual(other.NodeAddresses, new FastByteArrayComparer());
	}

	public override int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(Identifier);
		hashCode.Add(Type);
		hashCode.AddBytes(Address);
		hashCode.AddBytes(MegapoolAddress);

		if (ValidatorPubKey is not null)
		{
			hashCode.AddBytes(ValidatorPubKey);
		}

		hashCode.Add(ValidatorIndex);
		hashCode.Add(MegapoolIndex);
		hashCode.Add(AddressEnsName, StringComparer.OrdinalIgnoreCase);

		foreach (var address in NodeAddresses)
		{
			hashCode.AddBytes(address);
		}

		return hashCode.ToHashCode();
	}
}