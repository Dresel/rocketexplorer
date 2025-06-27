namespace RocketExplorer.Web.Pages;

public static class HttpRequestExtensions
{
	public static async Task<SnapshotResponse<T>> GetSnapshotResponse<T>(
		this HttpClient httpClient, string uri, CancellationToken cancellationToken = default)
	{
		using HttpRequestMessage snapshotRequest = new(HttpMethod.Get, uri);

		HttpResponseMessage snapshotResponse = await httpClient.SendAsync(
			snapshotRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

		return new SnapshotResponse<T>(snapshotResponse);
	}
}