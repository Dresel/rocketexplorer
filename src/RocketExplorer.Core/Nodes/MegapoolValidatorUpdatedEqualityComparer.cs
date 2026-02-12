namespace RocketExplorer.Core.Nodes;

public class
	MegapoolValidatorUpdatedEqualityComparer : IEqualityComparer<(string NodeAddress, string MegapoolAddress, int
	MegapoolIndex)>
{
	public bool Equals(
		(string NodeAddress, string MegapoolAddress, int MegapoolIndex) x,
		(string NodeAddress, string MegapoolAddress, int MegapoolIndex) y) =>
		string.Equals(x.NodeAddress, y.NodeAddress, StringComparison.OrdinalIgnoreCase)
		&& string.Equals(x.MegapoolAddress, y.MegapoolAddress, StringComparison.OrdinalIgnoreCase)
		&& x.MegapoolIndex == y.MegapoolIndex;

	public int GetHashCode((string NodeAddress, string MegapoolAddress, int MegapoolIndex) obj) =>
		HashCode.Combine(
			StringComparer.OrdinalIgnoreCase.GetHashCode(obj.NodeAddress),
			StringComparer.OrdinalIgnoreCase.GetHashCode(obj.MegapoolAddress),
			obj.MegapoolIndex);
}