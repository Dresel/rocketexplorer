namespace RocketExplorer.Core.Ens;

public class AddressEnsProcessHistory
{
	private readonly HashSet<AddressEnsRecord> addressEnsCollection = [];

	private readonly SemaphoreSlim semaphoreSlim = new(1, 1);

	public IEnumerable<AddressEnsRecord> ProcessedAddressEnsCandidates => this.addressEnsCollection;

	public async Task AddAddressEnsRecordAsync(
		byte[] address, string? ens = null, CancellationToken cancellationToken = default)
	{
		await this.semaphoreSlim.WaitAsync(cancellationToken);

		this.addressEnsCollection.Add(
			new AddressEnsRecord
			{
				Address = address,
				Ens = ens,
			});

		this.semaphoreSlim.Release();
	}
}