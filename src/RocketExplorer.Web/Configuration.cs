using Microsoft.AspNetCore.Components;

namespace RocketExplorer.Web;

public class Configuration
{
	public Configuration(NavigationManager navigation, ILogger<Configuration> configuration)
	{
		Uri uri = new(navigation.Uri);

		string subdomain = uri.Host.Split('.').First();

		Environment = subdomain switch
		{
			"devnet" => Environment.Devnet,
			"holesky" => Environment.Holesky,
			_ => Environment.Mainnet,
		};

		Network = Environment switch
		{
			Environment.Devnet => Network.Holesky,
			Environment.Holesky => Network.Holesky,
			Environment.Mainnet => Network.Mainnet,
			_ => throw new InvalidOperationException("Network is null"),
		};

		configuration.LogInformation(
			"Using Network {Network} and Rocket Pool Environment {Environment}", Network, Environment);
	}

	public Environment Environment { get; }

	public string EtherscanPrefix => Environment == Environment.Holesky ? "holesky." : string.Empty;

	public Network Network { get; }
}