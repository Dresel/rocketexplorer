using MessagePack;

namespace RocketExplorer.Web.Pages;

public class SnapshotResponse<T>(HttpResponseMessage httpResponseMessage) : IDisposable
{
	private readonly HttpResponseMessage httpResponseMessage = httpResponseMessage;

	public string? ETag => this.httpResponseMessage.Headers.ETag?.Tag;

	public bool IsSuccess => this.httpResponseMessage.IsSuccessStatusCode;

	public void Dispose() => this.httpResponseMessage.Dispose();

	public async Task<Snapshot<T>> ToSnapshotAsync(CancellationToken cancellationToken = default) =>
		new()
		{
			ETag = ETag,
			Data = await MessagePackSerializer.DeserializeAsync<T>(
				await this.httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken),
				MessagePackSerializerOptions.Standard, cancellationToken),
		};
}