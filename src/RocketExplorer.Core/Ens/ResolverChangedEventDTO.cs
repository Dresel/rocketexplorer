using Nethereum.ABI.FunctionEncoding.Attributes;

namespace RocketExplorer.Core.Ens;

[Event("ResolverChanged")]
public class ResolverChangedEventDTO : IEventDTO
{
	[Parameter("bytes32", "node", 1, true)]
	public byte[] Node { get; set; } = null!;

	[Parameter("address", "resolver", 2, false)]
	public string Resolver { get; set; } = null!;
}