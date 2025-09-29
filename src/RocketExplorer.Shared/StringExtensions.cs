namespace RocketExplorer.Shared;

public static class StringExtensions
{
	public static HashSet<string> NGrams(this string value)
	{
		HashSet<string> ngrams = [];

		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidOperationException("Value must not be null or white space");
		}

		string mappedValue = value.ToLowerInvariant().Map();

		for (int i = 0; i <= mappedValue.Length - 4; i++)
		{
			ngrams.Add(mappedValue[i..(i + 4)]);
		}

		return ngrams;
	}

	private static string Map(this string input)
	{
		char[] characters = new char[input.Length];

		for (int i = 0; i < input.Length; i++)
		{
			char c = input[i];
			characters[i] = c is >= 'a' and <= 'z' ? c : c is >= '0' and <= '9' ? c : 'z';
		}

		return new string(characters);
	}
}