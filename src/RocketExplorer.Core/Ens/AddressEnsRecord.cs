namespace RocketExplorer.Core.Ens;

public record class AddressEnsRecord
{
	public required byte[] Address { get; init; }

	public required string? Ens { get; init; }

	public virtual bool Equals(AddressEnsRecord? other)
	{
		if (other is null)
		{
			return false;
		}

		return string.Equals(Ens, other.Ens, StringComparison.OrdinalIgnoreCase)
			&& Address.SequenceEqual(other.Address);
	}

	public override int GetHashCode()
	{
		HashCode hashCode = default;

		hashCode.Add(Ens, StringComparer.OrdinalIgnoreCase);
		hashCode.AddBytes(Address);

		return hashCode.ToHashCode();
}
}