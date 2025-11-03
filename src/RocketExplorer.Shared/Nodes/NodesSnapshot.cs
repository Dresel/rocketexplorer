using MessagePack;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class NodesSnapshot
{
	[Key(0)]
	public required NodeIndexEntry[] Index { get; init; }

	[Key(1)]
	public required SortedList<DateOnly, int> DailyRegistrations { get; init; }

	[Key(2)]
	public required SortedList<DateOnly, int> TotalNodeCount { get; init; }
}

[MessagePackObject]
public record class NodesExtendedSnapshot
{
	[Key(0)]
	public required Dictionary<byte[], byte[]?> WithdrawalAddresses { get; set; }

	[Key(1)]
	public required Dictionary<byte[], byte[]?> RPLWithdrawalAddresses { get; set; }

	[Key(2)]
	public required Dictionary<byte[], List<byte[]>> StakeOnBehalfAddresses { get; set; }
}

//[MessagePackFormatter(typeof(ByteKeyFormatter))]
//public record class EthereumAddress
//{
//	public EthereumAddress(byte[]? bytes)
//	{
//		if (bytes?.Length != 20)
//		{
//			throw new ArgumentException("Address must be exactly 20 bytes long.");
//		}

//		this.Bytes = bytes;
//	}

//	public byte[] Bytes { get; }


//}