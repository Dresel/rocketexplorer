using Microsoft.AspNetCore.Components;

namespace RocketExplorer.Web.Pages;

public abstract class PageBase<T> : ComponentBase, IDisposable
{
	private readonly TaskCompletionSource taskCompletionSource = new();

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

	protected string ObjectStoreUrl =>
		!string.IsNullOrWhiteSpace(ObjectStoreKey) ? GetObjectStoreUrl(ObjectStoreKey) : string.Empty;

	protected Snapshot<T>? Snapshot { get; set; }

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			AppState.OnAppStateChanged -= OnAppStateChanged;
		}
	}

	protected string GetObjectStoreUrl(string key) => $"{Configuration.ObjectStoreBaseUrl}/{key}";

	protected async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ObjectStoreKey))
		{
			Logger.LogWarning("ObjectStoreKey is null or whitespace");
			return;
		}

		// TODO: Polly
		SnapshotResponse<T> response = await HttpClient.GetSnapshotResponse<T>(ObjectStoreUrl, cancellationToken);

		// Check manually to avoid additional render cycles
		if (Snapshot is null || string.IsNullOrWhiteSpace(response.ETag) || Snapshot.ETag != response.ETag)
		{
			Snapshot = await response.ToSnapshotAsync(cancellationToken);

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

	protected virtual void OnAppStateChanged(object? sender, AppState e) =>
		LoadAsync().ContinueWith(async _ => await InvokeAsync(StateHasChanged));
}