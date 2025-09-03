using MessagePack;
using MessagePack.Formatters;

namespace RocketExplorer.Shared;

public sealed class HexStringWithPrefixFormatter : IMessagePackFormatter<string?>
{
	public void Serialize(ref MessagePackWriter writer, string? value, MessagePackSerializerOptions options)
	{
		if (value is null)
		{
			throw new ArgumentNullException();
		}

		if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			value = value[2..];
		}

		writer.Write(Convert.FromHexString(value));
	}

	public string Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) =>
		$"0X{Convert.ToHexString(reader.ReadBytes()!.Value.FirstSpan)}";
}