namespace RocketExplorer.Core.Nodes;

public class MegapoolIndexEqualityComparer : IEqualityComparer<(string Address, int Index)>
{
	public bool Equals((string Address, int Index) x, (string Address, int Index) y)
	{
		return string.Equals(x.Address, y.Address, StringComparison.OrdinalIgnoreCase) && x.Index == y.Index;
	}

	public int GetHashCode((string Address, int Index) obj)
	{
		return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Address), obj.Index);
	}
}