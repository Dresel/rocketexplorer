using System.Buffers;
using System.Numerics;
using MessagePack;
using MessagePack.Formatters;

namespace RocketExplorer.Shared;

public class BigIntegerFormatter : IMessagePackFormatter<BigInteger>
{
	public void Serialize(ref MessagePackWriter writer, BigInteger value, MessagePackSerializerOptions options)
	{
		byte[] bytes = value.ToByteArray();
		writer.Write(bytes);
	}

	public BigInteger Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		byte[]? bytes = reader.ReadBytes()?.ToArray();
		return bytes == null ? BigInteger.Zero : new BigInteger(bytes);
	}
}