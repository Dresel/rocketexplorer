using Nethereum.Hex.HexTypes;

namespace RocketExplorer.Core;

public readonly struct EventIndex(HexBigInteger blockNumber, HexBigInteger logIndex)
	: IComparable<EventIndex>, IEquatable<EventIndex>
{
	public readonly HexBigInteger BlockNumber = blockNumber;
	public readonly HexBigInteger LogIndex = logIndex;

	private static long localLogIndex;

	public static EventIndex Next => new(new HexBigInteger(0), new HexBigInteger(Interlocked.Increment(ref localLogIndex)));

	public static bool operator ==(EventIndex a, EventIndex b)
	{
		return a.Equals(b);
	}

	public static bool operator !=(EventIndex a, EventIndex b)
	{
		return !a.Equals(b);
	}

	public static bool operator <(EventIndex a, EventIndex b)
	{
		return a.CompareTo(b) < 0;
	}

	public static bool operator >(EventIndex a, EventIndex b)
	{
		return a.CompareTo(b) > 0;
	}

	public static bool operator <=(EventIndex a, EventIndex b)
	{
		return a.CompareTo(b) <= 0;
	}

	public static bool operator >=(EventIndex a, EventIndex b)
	{
		return a.CompareTo(b) >= 0;
	}

	public int CompareTo(EventIndex other)
	{
		int c = this.BlockNumber.Value.CompareTo(other.BlockNumber);
		return c != 0 ? c : this.LogIndex.Value.CompareTo(other.LogIndex);
	}

	public bool Equals(EventIndex other) =>
		this.BlockNumber == other.BlockNumber && this.LogIndex == other.LogIndex;

	public override bool Equals(object? obj) =>
		obj is EventIndex o && Equals(o);

	public override int GetHashCode() =>
		HashCode.Combine(this.BlockNumber, this.LogIndex);

	public override string ToString() => $"{this.BlockNumber}:{this.LogIndex}";
}