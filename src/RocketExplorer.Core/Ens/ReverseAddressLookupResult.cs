using Nethereum.Contracts.Standards.ENS;

namespace RocketExplorer.Core.Ens;

public class ReverseAddressLookupResult
{
	public required byte[] AddressReverseNameHash { get; set; }

	public required string? ReverseResolvedEnsName { get; init; }

	public required byte[]? ReverseResolvedEnsNameHash { get; init; }

	public required PublicResolverService? ReverseResolver { get; init; }

	public required PublicResolverService? ForwardResolver { get; init; }

	public required byte[]? ForwardResolvedAddressReverseNameHash { get; set; }

	public bool IsValidPrimary => AddressReverseNameHash.SequenceEqual(ForwardResolvedAddressReverseNameHash);
}