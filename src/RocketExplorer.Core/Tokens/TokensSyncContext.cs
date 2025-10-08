namespace RocketExplorer.Core.Tokens;

public record class TokensSyncContext : ContextBase
{
	public required string[] PostSaturn1RocketNodeStakingAddresses { get; set; }

	public required string[] PreSaturn1RocketNodeStakingAddresses { get; set; }

	public string RETHTokenAddress => Contracts["rocketTokenRETH"].Versions.Select(x => x.Address).Single();

	public required TokenInfo RETHTokenInfo { get; init; }

	public static string RockRETHTokenAddress => "0x936faCdf10c8c36294e7b9d28345255539d81bc7";

	public required TokenInfo RockRETHTokenInfo { get; init; }

	public string RPLOldTokenAddress => Contracts["rocketTokenRPLFixedSupply"].Versions.Select(x => x.Address).Single();

	public required RPLOldTokenInfo RPLOldTokenInfo { get; init; }

	public string RPLTokenAddress => Contracts["rocketTokenRPL"].Versions.Select(x => x.Address).Single();

	public required TokenInfo RPLTokenInfo { get; init; }

	public required StakedRPLInfo StakedRPLInfo { get; set; }
}