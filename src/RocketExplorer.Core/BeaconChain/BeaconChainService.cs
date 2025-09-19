using System.Net;
using System.Net.Http.Json;
using Nethereum.Hex.HexConvertors.Extensions;

namespace RocketExplorer.Core.BeaconChain;

public class BeaconChainService(HttpClient httpClient)
{
	private readonly HttpClient httpClient = httpClient;

	public async Task<long?> GetValidatorIndex(byte[] pubKey)
	{
		string query = $"eth/v1/beacon/states/head/validators/{pubKey.ToHex(true)}";

		try
		{
			ValidatorResult result = await this.httpClient.GetFromJsonAsync<ValidatorResult>(query) ??
				throw new InvalidOperationException();

			return result.Data.Index;
		}
		catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	public async Task<ValidatorsResult> GetValidators(IEnumerable<byte[]> pubKeys)
	{
		string query =
			$"eth/v1/beacon/states/head/validators?{string.Join("&", pubKeys.Select(x => $"id={x.ToHex(true)}"))}";

		return await this.httpClient.GetFromJsonAsync<ValidatorsResult>(query) ?? throw new InvalidOperationException();
	}
}