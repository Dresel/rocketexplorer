namespace RocketExplorer.Core;

public class ProcessHistory
{
	//private readonly HashSet<byte[]> addresses = new(new FastByteArrayComparer());
	private readonly HashSet<AddressEnsCandidate> addressEnsCollection = [];

	private readonly SemaphoreSlim semaphoreSlim = new(1, 1);

	//public IEnumerable<byte[]> ProcessedAddresses => this.addresses;

	public IEnumerable<AddressEnsCandidate> ProcessedAddressEnsCandidates => this.addressEnsCollection;

	//public async Task AddAddressAsync(byte[] address, CancellationToken cancellationToken = default)
	//{
	//	await this.semaphoreSlim.WaitAsync(cancellationToken);

	//	this.addresses.Add(address);

	//	this.semaphoreSlim.Release();
	//}

	public async Task AddAddressEnsCandidateAsync(byte[] address, string? ens = null, CancellationToken cancellationToken = default)
	{
		await this.semaphoreSlim.WaitAsync(cancellationToken);

		this.addressEnsCollection.Add(new()
		{
			Address = address,
			Ens = ens,
		});

		this.semaphoreSlim.Release();
	}

	public void Clear() => this.addressEnsCollection.Clear();
}

public record class AddressEnsCandidate
{
	public required byte[] Address { get; init; }

	public required string? Ens { get; init; }

	public virtual bool Equals(AddressEnsCandidate? other)
	{
		if (other is null)
		{
			return false;
		}

		return string.Equals(Ens, other.Ens, StringComparison.OrdinalIgnoreCase)
			&& Address.SequenceEqual(other.Address);
	}

	public override int GetHashCode() => HashCode.Combine(
		StringComparer.OrdinalIgnoreCase.GetHashCode(Ens ?? string.Empty), HashCode.Combine(Address));
}