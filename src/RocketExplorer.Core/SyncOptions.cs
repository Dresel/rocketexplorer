namespace RocketExplorer.Core;

public class SyncOptions
{
	public required string Environment { get; init; }

	public required string RPCUrl { get; init; }

	public required string RocketStorageContractAddress { get; init; }
}