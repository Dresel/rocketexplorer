using System.Globalization;

namespace RocketExplorer.Shared;

public static class Keys
{
	public const string ContractsSnapshotKey = "contracts-snapshot.msgpack";

	public const string DashboardSnapshot = "dashboard-snapshot.msgpack";

	public const string NodesSnapshot = "nodes-snapshot.msgpack";

	public const string QueueSnapshot = "queue-snapshot.msgpack";

	public const string SnapshotMetadata = "snapshot-metadata.msgpack";

	public const string TokensRETHSnapshot = "tokens-reth-snapshot.msgpack";

	public const string TokensRPLOldSnapshot = "tokens-rpl-old-snapshot.msgpack";

	public const string TokensRPLSnapshot = "tokens-rpl-snapshot.msgpack";

	public const string TokensStakedRPLSnapshot = "tokens-staked-rpl-snapshot.msgpack";

	public const string ValidatorSnapshot = "validator-snapshot.msgpack";

	public static string MegapoolValidator(string megapoolAddress, int megapoolIndex) =>
		$"validators/{megapoolAddress.ToLowerInvariant()}/{megapoolIndex.ToString(CultureInfo.InvariantCulture)}.msgpack";

	public static string MinipoolValidator(string minipoolAddress) =>
		$"validators/{minipoolAddress.ToLowerInvariant()}.msgpack";

	public static string Node(string nodeAddress) => $"nodes/{nodeAddress.ToLowerInvariant()}.msgpack";
}