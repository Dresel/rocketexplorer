namespace RocketExplorer.Core;

public static class CollectionExtensions
{
	public static void ReplaceAll<T>(this IList<T> elements, Predicate<T> predicate, Func<T, T> replaceFunc)
	{
		for (int i = 0; i < elements.Count; i++)
		{
			if (predicate(elements[i]))
			{
				elements[i] = replaceFunc(elements[i]);
			}
		}
	}

	public static void ReplaceWhere<T>(this T[] elements, Predicate<T> predicate, Func<T, T> replaceFunc)
	{
		int index = Array.FindIndex(elements, predicate);

		if (index == -1)
		{
			throw new InvalidOperationException("Element not found");
		}

		elements[index] = replaceFunc(elements[index]);
	}
}