namespace RocketExplorer.Web;

public static class TimespanExtensions
{
	public static string ToReadableString(this TimeSpan timeSpan)
	{
		List<string> parts = new();

		if (timeSpan.Days > 0)
		{
			parts.Add($"{timeSpan.Days} day{(timeSpan.Days > 1 ? "s" : string.Empty)}");
		}

		if (timeSpan.Hours > 0)
		{
			parts.Add($"{timeSpan.Hours} hour{(timeSpan.Hours > 1 ? "s" : string.Empty)}");
		}

		if (timeSpan is { Minutes: > 0, Days: 0 })
		{
			parts.Add($"{timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : string.Empty)}");
		}

		if (timeSpan is { Seconds: > 0, Days: 0, Hours: 0 })
		{
			parts.Add($"{timeSpan.Seconds} second{(timeSpan.Seconds > 1 ? "s" : string.Empty)}");
		}

		return parts.Count == 0 ? "0 seconds ago" : string.Join(" and ", parts);
	}
}