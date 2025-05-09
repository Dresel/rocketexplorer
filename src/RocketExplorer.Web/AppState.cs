using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Shared;

namespace RocketExplorer.Web;

public class AppState
{
	public event EventHandler<AppState>? OnAppStateChanged;

	public long? BlockDifference => (long?)CurrentBlock?.Number.Value - SnapshotMetadata?.BlockNumber;

	public Block? CurrentBlock { get; private set; }

	public long? ProcessedBlockNumber => SnapshotMetadata?.BlockNumber;

	public SnapshotMetadata? SnapshotMetadata { get; private set; }

	public void Set(Block block, SnapshotMetadata metadata)
	{
		CurrentBlock = block;
		SnapshotMetadata = metadata;

		OnAppStateChanged?.Invoke(this, this);
	}
}