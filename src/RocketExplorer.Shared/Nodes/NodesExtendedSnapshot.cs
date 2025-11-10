using MessagePack;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class NodesExtendedSnapshot
{
	[Key(0)]
	public required Dictionary<byte[], byte[]> WithdrawalAddresses { get; set; }

	[Key(1)]
	public required Dictionary<byte[], byte[]> RPLWithdrawalAddresses { get; set; }

	[Key(2)]
	public required Dictionary<byte[], HashSet<byte[]>> StakeOnBehalfAddresses { get; set; }
}