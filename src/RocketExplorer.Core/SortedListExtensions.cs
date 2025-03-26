namespace RocketExplorer.Core;

public static class SortedListExtensions
{
	public static void AddOrUpdate<TKey, TValue>(this SortedList<TKey, TValue> sortedList, TKey key, TValue value)
		where TKey : notnull
	{
		if (!sortedList.TryAdd(key, value))
		{
			sortedList[key] = value;
		}
	}

	public static TValue? GetValueOrLastOrDefault<TKey, TValue>(this SortedList<TKey, TValue> sortedList, TKey key)
		where TKey : notnull
	{
		if (sortedList.Count == 0)
		{
			return default;
		}

		if (sortedList.TryGetValue(key, out TValue? latest))
		{
			return latest;
		}

		return sortedList.Values[^1];
	}
}