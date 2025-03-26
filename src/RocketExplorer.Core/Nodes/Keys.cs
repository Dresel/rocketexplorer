namespace RocketExplorer.Core.Nodes;

public static class Keys
{
	public const string NodesSnapshot = "nodes-snapshot.msgpack";

	public const string QueueSnapshot = "queue-snapshot.msgpack";

	public static string MegapoolMinipool(string megapoolAddress, int megapoolIndex) =>
		$"minipools/{megapoolAddress}/{megapoolIndex}.msgpack";

	public static string Node(string nodeAddress) => $"nodes/{nodeAddress}.msgpack";
}