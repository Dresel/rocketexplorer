using System.Text;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using RocketExplorer.Ethereum.RocketStorage;

namespace RocketExplorer.Ethereum;

public static class RocketStorageServiceExtensions
{
	public static byte[] AsContractAddressParameter(this string contractName) =>
		$"contract.address{contractName}".Sha3();

	public static Task<string> GetAddressQueryAsync(
		this RocketStorageService rocketStorageService, string contractName, BlockParameter? blockParameter = null) =>
		rocketStorageService.GetAddressQueryAsync(contractName.AsContractAddressParameter(), blockParameter);

	public static byte[] Sha3(this string value) =>
		new Sha3Keccack().CalculateHash(Encoding.UTF8.GetBytes(value));
}