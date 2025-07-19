using Microsoft.AspNetCore.Components;

namespace RocketExplorer.Web;

public class Configuration
{
	public Configuration(NavigationManager navigation, ILogger<Configuration> logger)
	{
		Uri uri = new(navigation.Uri);

		string subdomain = uri.Host.Split('.').First();

		Environment = subdomain switch
		{
			"devnet" => Environment.Devnet,
			"testnet" => Environment.Testnet,
			_ => Environment.Mainnet,
		};

		Network = Environment switch
		{
			Environment.LocalDevnet => Network.Hoodi,
			Environment.Devnet => Network.Hoodi,
			Environment.Testnet => Network.Hoodi,
			Environment.LocalMainnet => Network.Mainnet,
			Environment.Mainnet => Network.Mainnet,
			_ => throw new InvalidOperationException("Network is null"),
		};

		logger.LogInformation(
			"Using Network {Network} and Rocket Pool Environment {Environment}", Network, Environment);
	}

	public Environment Environment { get; }

	// TODO: Load from configuration?
	public string EthereumRPCEndpoint => Network switch
	{
		Network.Hoodi => "https://ethereum-hoodi-rpc.publicnode.com",
		Network.Mainnet => "https://ethereum-rpc.publicnode.com",
		_ => throw new InvalidOperationException("RPCUrl is null"),
	};

	public string EtherscanPrefix => Network == Network.Hoodi ? "hoodi." : string.Empty;

	public Network Network { get; }

	// TODO: Load from configuration?
	public string ObjectStoreBaseUrl => $"https://rocketexplorer.nbg1.your-objectstorage.com/{ObjectStoreBucketName}";

	public string ObjectStoreBucketName =>
		Environment switch
		{
			Environment.LocalDevnet => "local-devnet",
			Environment.Devnet => "devnet",
			Environment.Testnet => "testnet",
			Environment.LocalMainnet => "local-mainnet",
			Environment.Mainnet => "mainnet",
			_ => throw new ArgumentOutOfRangeException(),
		};
}