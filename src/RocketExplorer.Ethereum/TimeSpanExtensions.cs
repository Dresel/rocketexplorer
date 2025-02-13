namespace RocketExplorer.Ethereum;

public static class TimeSpanExtensions
{
	private const int SecondsPerBlock = 12;

	public static uint BlockCount(this TimeSpan timeSpan) => (uint)(timeSpan.TotalSeconds / SecondsPerBlock);
}