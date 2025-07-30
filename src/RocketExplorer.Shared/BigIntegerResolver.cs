using System.Numerics;
using MessagePack;
using MessagePack.Formatters;

namespace RocketExplorer.Shared;

public class BigIntegerResolver : IFormatterResolver
{
	public static readonly IFormatterResolver Instance = new BigIntegerResolver();

	private readonly BigIntegerFormatter formatter = new();

	public IMessagePackFormatter<T>? GetFormatter<T>()
	{
		if (typeof(T) == typeof(BigInteger))
		{
			return (IMessagePackFormatter<T>)this.formatter;
		}

		return null;
	}
}