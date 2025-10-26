using Nethereum.Contracts.Standards.ENS;

namespace RocketExplorer.Core.Ens;

public class ForwardResolutionResult
{
	// namehash(name.eth)
	public required byte[] EnsNameHash { get; init; }

	public required PublicResolverService? ForwardResolver { get; init; }

	public required string? ResolvedAddress { get; set; }

	public required byte[]? ResolvedAddressReverseNameHash { get; set; }
}