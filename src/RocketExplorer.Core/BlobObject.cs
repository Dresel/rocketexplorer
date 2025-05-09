namespace RocketExplorer.Core;

public class BlobObject<T>
{
	public required long ProcessedBlockNumber { get; init; }

	public required T Data { get; init; }
}