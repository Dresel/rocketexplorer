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

	public static TValue? GetLatestOrDefault<TKey, TValue>(this SortedList<TKey, TValue> sortedList)
		where TKey : notnull
	{
		if (sortedList.Count == 0)
		{
			return default;
		}

		return sortedList.Values[^1];
	}
}