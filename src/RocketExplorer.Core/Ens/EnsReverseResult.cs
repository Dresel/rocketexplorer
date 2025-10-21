using Nethereum.Contracts.Standards.ENS;

namespace RocketExplorer.Core.Ens;

public class EnsReverseResult
{
	public required string? ResolvedEnsName { get; set; }

	// namehash(<address>.addr.reverse)
	public required byte[] ReverseNameHash { get; init; }

	public required PublicResolverService? ReverseResolver { get; init; }

	public required byte[]? ResolvedEnsNameHash { get; set; }
}