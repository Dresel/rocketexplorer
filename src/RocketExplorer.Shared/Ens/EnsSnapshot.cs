using MessagePack;

namespace RocketExplorer.Shared.Ens;

[MessagePackObject]
public class EnsSnapshot
{
	[Key(0)]
	public required Dictionary<byte[], string> AddressEnsMap { get; set; }
}