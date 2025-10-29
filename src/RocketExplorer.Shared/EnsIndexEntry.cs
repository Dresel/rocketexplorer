using MessagePack;

namespace RocketExplorer.Shared;

[MessagePackObject]
public record class EnsIndexEntry
{
	[Key(0)]
	public required IndexEntryType Type { get; init; }

	[Key(1)]
	public required byte[] Address { get; init; }

	[Key(2)]
	public required string AddressEnsName { get; init; }

	public virtual bool Equals(EnsIndexEntry? other)
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
			string.Equals(AddressEnsName, other.AddressEnsName, StringComparison.OrdinalIgnoreCase);
	}

	public override int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.Add(Type);
		hashCode.AddBytes(Address);

		hashCode.Add(AddressEnsName, StringComparer.OrdinalIgnoreCase);

		return hashCode.ToHashCode();
	}
}