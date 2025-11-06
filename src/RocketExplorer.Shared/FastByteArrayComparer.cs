using System.Collections;

namespace RocketExplorer.Shared;

public class FastByteArrayComparer : IComparer<byte[]>, IEqualityComparer<byte[]>
{
	public int Compare(byte[]? x, byte[]? y)
	{
		if (ReferenceEquals(x, y))
		{
			return 0;
		}

		if (x is null)
		{
			return -1;
		}

		if (y is null)
		{
			return 1;
		}

		return x.AsSpan().SequenceCompareTo(y);
	}

	public bool Equals(byte[]? x, byte[]? y)
	{
		if (x == null && y == null)
		{
			return true;
		}

		if (x == null || y == null)
		{
			return false;
		}

		return x.AsSpan().SequenceEqual(y);
	}

	public int GetHashCode(byte[]? value)
	{
		if (value == null)
		{
			return 0;
		}

		return StructuralComparisons.StructuralEqualityComparer.GetHashCode(value);
	}
}