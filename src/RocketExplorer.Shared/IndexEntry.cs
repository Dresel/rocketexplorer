using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class IndexEntry
{
	[Key(0)]
	public required IndexEntryType Type { get; init; }

	[Key(1)]
	public required byte[] Address { get; init; }

	[Key(2)]
	public required byte[]? MegapoolAddress { get; init; }

	[Key(3)]
	public required byte[]? ValidatorPubKey { get; init; }

	[Key(4)]
	public required long? ValidatorIndex { get; init; }

	[Key(5)]
	public required int? MegapoolIndex { get; init; }

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

		return Type == other.Type &&
			Address.SequenceEqual(other.Address) &&
			((MegapoolAddress is null && other.MegapoolAddress is null) || (MegapoolAddress is not null &&
				other.MegapoolAddress is not null && MegapoolAddress.SequenceEqual(other.MegapoolAddress))) &&
			((ValidatorPubKey is null && other.ValidatorPubKey is null) || (ValidatorPubKey is not null &&
				other.ValidatorPubKey is not null && ValidatorPubKey.SequenceEqual(other.ValidatorPubKey))) &&
			ValidatorIndex == other.ValidatorIndex && MegapoolIndex == other.MegapoolIndex;
	}

	public override int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.Add(Type);
		hashCode.AddBytes(Address);

		if (ValidatorPubKey is not null)
		{
			hashCode.AddBytes(ValidatorPubKey);
		}

		hashCode.Add(ValidatorIndex);
		hashCode.Add(MegapoolIndex);

		return hashCode.ToHashCode();
	}
}