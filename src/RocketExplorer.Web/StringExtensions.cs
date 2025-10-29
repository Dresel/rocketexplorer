namespace RocketExplorer.Web;

public static class StringExtensions
{
	public static string Ellipsize(this string text, int prefixLength, int suffixLength) =>
		prefixLength + suffixLength < text.Length ? $"{text[..prefixLength]}...{text[^suffixLength..]}" : text;

	public static IEnumerable<string> ExtractHighlightTexts(
		this string text, string highlightedText, int prefixLength, int suffixLength)
	{
		if (string.IsNullOrWhiteSpace(highlightedText))
		{
			yield break;
		}

		foreach (int start in AllIndexesOf(text, highlightedText))
		{
			int end = start + highlightedText.Length;

			if (start < text.Length - suffixLength && prefixLength < end)
			{
				bool overlapsPrefix = start < prefixLength;
				bool overlapsSuffix = end > text.Length - suffixLength;

				if (overlapsPrefix && overlapsSuffix)
				{
					yield return $"{text[start..Math.Min(end, prefixLength)]}...{text[^suffixLength..end]}";
				}
				else if (overlapsPrefix)
				{
					yield return $"{text[start..Math.Min(end, prefixLength)]}...";
				}
				else if (overlapsSuffix)
				{
					yield return $"...{text[^suffixLength..end]}";
				}
				else
				{
					yield return "...";
				}
			}
			else
			{
				yield return highlightedText;
			}
		}
	}

	public static int ExtractIndexOf(this string text, string highlightedText, int prefixLength, int suffixLength)
	{
		int start = AllIndexesOf(text, highlightedText).First();

		int end = start + highlightedText.Length;

		if (start < text.Length - suffixLength && prefixLength < end)
		{
			bool overlapsPrefix = start < prefixLength;

			if (overlapsPrefix)
			{
				return start;
			}

			return prefixLength;
		}

		return start;
	}

	private static IEnumerable<int> AllIndexesOf(string @string, string value)
	{
		for (int index = 0; true; index += value.Length)
		{
			index = @string.IndexOf(value, index, StringComparison.OrdinalIgnoreCase);
			if (index == -1)
			{
				break;
			}

			yield return index;
		}
	}
}