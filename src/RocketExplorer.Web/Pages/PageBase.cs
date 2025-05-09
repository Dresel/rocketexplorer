using MessagePack;
using Microsoft.AspNetCore.Components;

namespace RocketExplorer.Web.Pages;

public abstract class PageBase<T> : ComponentBase
{
	private readonly TaskCompletionSource taskCompletionSource = new();

	private string? eTag;

	[Inject]
	protected AppState AppState { get; set; } = null!;

	[Inject]
	protected Configuration Configuration { get; set; } = null!;

	[Inject]
	protected HttpClient HttpClient { get; set; } = null!;

	protected bool IsLoaded => this.taskCompletionSource.Task.IsCompletedSuccessfully;

	protected bool IsLoading => !IsLoaded;

	protected Task LoadedTask => this.taskCompletionSource.Task;

	[Inject]
	protected ILogger<PageBase<T>> Logger { get; set; } = null!;

	protected string? ObjectStoreKey { get; set; }

	protected string ObjectStoreUrl => $"{Configuration.ObjectStoreBaseUrl}/{ObjectStoreKey}";

	protected T? Snapshot { get; set; }

	protected async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ObjectStoreKey))
		{
			Logger.LogWarning("ObjectStoreKey is null or whitespace");
			return;
		}

		// TODO: Polly
		using HttpRequestMessage snapshotRequest = new(HttpMethod.Head, ObjectStoreUrl);
		using HttpResponseMessage snapshotResponse = await HttpClient.SendAsync(
			snapshotRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		snapshotResponse.EnsureSuccessStatusCode();

		string? latestETag = snapshotResponse.Headers.ETag?.Tag;

		if (string.IsNullOrWhiteSpace(this.eTag) || string.IsNullOrWhiteSpace(latestETag) || this.eTag != latestETag)
		{
			this.eTag = latestETag;

			Snapshot = MessagePackSerializer.Deserialize<T>(
				await HttpClient.GetStreamAsync(ObjectStoreUrl, cancellationToken),
				MessagePackSerializerOptions.Standard);
			await OnAfterSnapshotLoadedAsync(cancellationToken);
			this.taskCompletionSource.TrySetResult();
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			await LoadAsync();
			this.taskCompletionSource.TrySetResult();

			await InvokeAsync(StateHasChanged);

			AppState.OnAppStateChanged += OnAppStateChanged;
		}
	}

	protected abstract Task OnAfterSnapshotLoadedAsync(CancellationToken cancellationToken = default);

	private void OnAppStateChanged(object? sender, AppState e) =>
		LoadAsync().ContinueWith(async _ => await InvokeAsync(StateHasChanged));
}