namespace RocketExplorer.Core.Tokens;

public class TokensSyncContext : ContextBase
{
	public required string[] PostSaturn1RocketNodeStakingAddresses { get; set; }

	public required string[] PreSaturn1RocketNodeStakingAddresses { get; set; }

	public string RETHTokenAddress => Contracts["rocketTokenRETH"].Versions.Select(x => x.Address).Single();

	public required TokenInfo RETHTokenInfo { get; init; }

	public string RPLOldTokenAddress => Contracts["rocketTokenRPLFixedSupply"].Versions.Select(x => x.Address).Single();

	public required TokenInfo RPLOldTokenInfo { get; init; }

	public string RPLTokenAddress => Contracts["rocketTokenRPL"].Versions.Select(x => x.Address).Single();

	public required RPLTokenInfo RPLTokenInfo { get; init; }
}