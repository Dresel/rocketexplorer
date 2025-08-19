using System.Numerics;
using Nethereum.Util;

namespace RocketExplorer.Web;

public static class BigIntegerExtensions
{
	public static decimal Normalize(this BigInteger amount) => UnitConversion.Convert.FromWei(amount, 18);
}