namespace RocketExplorer.Core.Nodes;

public class MinipoolValidatorUpdatedEqualityComparer : IEqualityComparer<(string NodeAddress, string MinipoolAddress)>
{
	public bool Equals((string NodeAddress, string MinipoolAddress) x, (string NodeAddress, string MinipoolAddress) y)
	{
		return string.Equals(x.NodeAddress, y.NodeAddress, StringComparison.OrdinalIgnoreCase)
			&& string.Equals(x.MinipoolAddress, y.MinipoolAddress, StringComparison.OrdinalIgnoreCase);
	}

	public int GetHashCode((string NodeAddress, string MinipoolAddress) obj)
	{
		return HashCode.Combine(
			StringComparer.OrdinalIgnoreCase.GetHashCode(obj.NodeAddress),
			StringComparer.OrdinalIgnoreCase.GetHashCode(obj.MinipoolAddress));
	}
}
