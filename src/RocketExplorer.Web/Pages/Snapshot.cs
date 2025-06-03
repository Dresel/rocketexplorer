namespace RocketExplorer.Web.Pages;

public record Snapshot<T>
{
	public required T Data { get; init; }

	public required string? ETag { get; init; }
}