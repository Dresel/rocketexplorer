using Nethereum.Contracts.Standards.ENS;
using Nethereum.Hex.HexConvertors.Extensions;

namespace RocketExplorer.Core.Ens;

public static class EnsUtilExtensions
{
	public static byte[] ToReverseAddressNameHash(this EnsUtil ensUtil, string address)
	{
		string reverseAddressName = address.RemoveHexPrefix().ToLower() + ENSService.REVERSE_NAME_SUFFIX;
		return ensUtil.GetNameHash(reverseAddressName).HexToByteArray();
	}
}