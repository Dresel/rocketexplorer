using Microsoft.AspNetCore.Components;

namespace RocketExplorer.Web;

public class Configuration
{
	public Configuration(NavigationManager navigation, ILogger<Configuration> configuration)
	{
		Uri uri = new(navigation.Uri);

		string subdomain = uri.Host.Split('.').First();

		if (new[] { "devnet", "holesky", "mainnet", }.Contains(subdomain))
		{
			Environment = subdomain;
		}

		Network = Environment switch
		{
			"devnet" => "holesky",
			"holesky" => "holesky",
			"mainnet" => "mainnet",
			_ => throw new InvalidOperationException("Network is null"),
		};

		configuration.LogInformation(
			"Using Network {Network} and Rocket Pool Environment {Environment}", Network, Environment);
	}

	public string Environment { get; } = "mainnet";

	public string EtherscanPrefix => Environment == "holesky" ? "holesky." : string.Empty;

	public string Network { get; }
}