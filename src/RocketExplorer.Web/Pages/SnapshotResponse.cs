using MessagePack;

namespace RocketExplorer.Web.Pages;

public class SnapshotResponse<T>(HttpResponseMessage httpResponseMessage) : IDisposable
{
	private readonly HttpResponseMessage httpResponseMessage = httpResponseMessage;

	public string? ETag => this.httpResponseMessage.Headers.ETag?.Tag;

	public void Dispose() => this.httpResponseMessage.Dispose();

	public async Task<Snapshot<T>> ToSnapshotAsync(CancellationToken cancellationToken) =>
		new()
		{
			ETag = ETag,
			Data = MessagePackSerializer.Deserialize<T>(
				await this.httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken),
				MessagePackSerializerOptions.Standard),
		};
}