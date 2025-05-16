using LiveChartsCore.Defaults;
using RocketExplorer.Web.Components;

namespace RocketExplorer.Web;

public static class EnumerableExtensions
{
	// See https://stackoverflow.com/a/2948872
	public static int BinarySearch<TItem, TSearch>(
		this ICollection<TItem> collection,
		TSearch value, Func<TSearch, TItem, int> comparer)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(comparer);

		int lower = 0;
		int upper = collection.Count - 1;

		while (lower <= upper)
		{
			int middle = lower + ((upper - lower) / 2);
			int comparisonResult = comparer(value, collection.ElementAt(middle));

			if (comparisonResult < 0)
			{
				upper = middle - 1;
			}
			else if (comparisonResult > 0)
			{
				lower = middle + 1;
			}
			else
			{
				return middle;
			}
		}

		return ~lower;
	}

	public static int BinarySearch<TItem>(this ICollection<TItem> collection, TItem value) =>
		BinarySearch(collection, value, Comparer<TItem>.Default);

	public static int BinarySearch<TItem>(
		this ICollection<TItem> collection, TItem value,
		IComparer<TItem> comparer) =>
		collection.BinarySearch(value, comparer.Compare);

	public static IEnumerable<KeyValuePair<DateOnly, int>> FillGaps(this IEnumerable<KeyValuePair<DateOnly, int>> data)
	{
		using IEnumerator<KeyValuePair<DateOnly, int>> enumerator = data.GetEnumerator();
		enumerator.MoveNext();

		KeyValuePair<DateOnly, int> previous = enumerator.Current;
		yield return previous;

		while (enumerator.MoveNext())
		{
			DateOnly currentDay = previous.Key.AddDays(1);

			while (currentDay < enumerator.Current.Key)
			{
				yield return new KeyValuePair<DateOnly, int>(currentDay, previous.Value);
				currentDay = currentDay.AddDays(1);
			}

			yield return enumerator.Current;
			previous = enumerator.Current;
		}
	}

	public static IEnumerable<IGrouping<DateOnly, KeyValuePair<DateOnly, int>>> GroupBy(
		this IEnumerable<KeyValuePair<DateOnly, int>> data, ChartAggregation aggregation) =>
		aggregation switch
		{
			ChartAggregation.Yearly => data.GroupBy(x => new DateOnly(x.Key.Year, 7, 1)),
			ChartAggregation.Monthly => data.GroupBy(x => new DateOnly(x.Key.Year, x.Key.Month, 16)),
			ChartAggregation.Daily => data.GroupBy(x => new DateOnly(x.Key.Year, x.Key.Month, x.Key.Day)),
			_ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, null),
		};

	public static (List<T> Items, int TotalItems) Paginate<T>(this IEnumerable<T> source, int page, int pageSize)
	{
		List<T> items = new();

		int totalCount = 0;
		int startIndex = (page - 1) * pageSize;
		int endIndex = startIndex + pageSize;

		foreach (T item in source)
		{
			if (totalCount >= startIndex && totalCount < endIndex)
			{
				items.Add(item);
			}

			totalCount++;
		}

		return (items, totalCount);
	}

	public static IEnumerable<DateTimePoint> PrepareDelta(
		this ICollection<KeyValuePair<DateOnly, int>> data, Func<int, int>? dataTransform, ChartAggregation aggregation, DateOnly start)
	{
		// Assuming data.Max(x => x.Key) <= DateTime.Now
		DateOnly end = DateOnly.FromDateTime(DateTime.Now);

		int startIndex = data.BinarySearch(start, (key, item) => key.CompareTo(item.Key));

		if (startIndex < 0)
		{
			startIndex = ~startIndex;
		}

		IEnumerable<KeyValuePair<DateOnly, int>> keyValuePairs = data.Skip(Math.Max(0, startIndex));

		return keyValuePairs.GroupBy(aggregation).Select(
			x =>
				new DateTimePoint(new DateTime(x.Key.Year, x.Key.Month, x.Key.Day), x.Sum(x => dataTransform?.Invoke(x.Value) ?? x.Value)));
	}

	public static IEnumerable<DateTimePoint> PrepareTotal(
		this ICollection<KeyValuePair<DateOnly, int>> data, ChartAggregation aggregation, DateOnly start)
	{
		//// Assuming data.Max(x => x.Key) <= DateTime.Now
		DateOnly end = DateOnly.FromDateTime(DateTime.Now);

		int startIndex = data.BinarySearch(start, (key, item) => key.CompareTo(item.Key));

		if (startIndex < 0)
		{
			startIndex = ~startIndex;
		}

		int endIndex = data.BinarySearch(end, (key, item) => key.CompareTo(item.Key));

		if (endIndex < 0)
		{
			endIndex = ~endIndex;
		}

		IEnumerable<KeyValuePair<DateOnly, int>> keyValuePairs = data.Skip(Math.Max(0, startIndex));

		if (startIndex == data.Count || startIndex - 1 < 0 || data.ElementAt(startIndex).Key != start)
		{
			keyValuePairs = keyValuePairs.Prepend(
				new KeyValuePair<DateOnly, int>(start, startIndex - 1 >= 0 ? data.ElementAt(startIndex - 1).Value : 0));
		}

		if (endIndex == data.Count || data.ElementAt(endIndex).Key != end)
		{
			keyValuePairs = keyValuePairs.Append(
				new KeyValuePair<DateOnly, int>(end, endIndex - 1 >= 0 ? data.ElementAt(endIndex - 1).Value : 0));
		}

		return keyValuePairs.FillGaps().GroupBy(aggregation).Select(
			x =>
				new DateTimePoint(new DateTime(x.Key.Year, x.Key.Month, x.Key.Day), x.Max(x => x.Value)));
	}
}