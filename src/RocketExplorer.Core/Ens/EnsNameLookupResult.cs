using Nethereum.Contracts.Standards.ENS;

namespace RocketExplorer.Core.Ens;

public class EnsNameLookupResult
{
	public required byte[] EnsNameHash { get; init; }

	public required string? ForwardResolvedAddress { get; set; }

	public required byte[]? ForwardResolvedAddressReverseNameHash { get; set; }

	public required PublicResolverService? ForwardResolver { get; init; }

	public bool IsValidPrimary => EnsNameHash.SequenceEqual(ReverseResolvedEnsNameHash ?? []);

	public required string? ReverseResolvedEnsName { get; init; }

	public required byte[]? ReverseResolvedEnsNameHash { get; set; }

	public required PublicResolverService? ReverseResolver { get; init; }
}