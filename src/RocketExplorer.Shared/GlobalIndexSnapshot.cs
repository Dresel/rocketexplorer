using MessagePack;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Shared;

[MessagePackObject]
public class GlobalIndexSnapshot
{
	[Key(0)]
	public required NodeIndexEntry[] Index { get; init; }

}

public class IndexEntry
{
	[Key(0)]
	public required IndexEntryType Type { get; set; }

	[Key(1)]
	public byte[]? Address { get; init; }

	[Key(2)]
	public int MegapoolIndex { get; set; }
}

[Flags]
public enum IndexEntryType : byte
{
	NodeOperator = 1,

	MinipoolValidator = 2,

	MegapoolValidator = 4,

	RETHHolder = 8,

	RPLHolder = 16,

	RPLOldHolder = 32,
}

public static class AddressExtensions
{
	public static ushort[] NGrams(this byte[] bytes)
	{
		HashSet<ushort> ngrams = [];

		for (int j = 0; j < (bytes.Length * 2) - 4; j++) // j = nibble index
		{
			int byteIndex = j >> 1;
			int bitOffset = (j & 1) * 4;
			int raw24 = (bytes[byteIndex] << 16)
				| (bytes[byteIndex + 1] << 8)
				| bytes[byteIndex + 2];

			ushort value = (ushort)((raw24 >> (8 - bitOffset)) & 0xFFFF);
			ngrams.Add(value);
		}

		return ngrams.ToArray();
	}
}