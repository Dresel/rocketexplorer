namespace RocketExplorer.Core;

public static class CollectionExtensions
{
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