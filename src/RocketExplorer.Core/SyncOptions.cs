namespace RocketExplorer.Core;

public class SyncOptions
{
	public required string BeaconChainUrl { get; set; } = string.Empty;

	public string BucketName { get; init; } = "rocketexplorer";

	public required string Environment { get; init; }

	public required string RocketStorageContractAddress { get; init; }

	public string RpcBasicAuthPassword { get; init; } = string.Empty;

	public string RpcBasicAuthUsername { get; init; } = string.Empty;

	public required string RPCUrl { get; init; }
}