using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace RocketExplorer.Web.Pages;

public abstract class PageBase<T> : ComponentBase, IDisposable
{
	private readonly SemaphoreSlim semaphore = new(1, 1);

	private readonly TaskCompletionSource taskCompletionSource = new();

	[Inject]
	protected AppState AppState { get; set; } = null!;

	[Inject]
	protected Configuration Configuration { get; set; } = null!;

	protected int DeserializeElapsedMilliseconds { get; private set; }

	[Inject]
	protected IWebAssemblyHostEnvironment HostEnvironment { get; set; } = null!;

	[Inject]
	protected HttpClient HttpClient { get; set; } = null!;

	protected bool IsLoaded => this.taskCompletionSource.Task.IsCompletedSuccessfully;

	protected bool IsLoading => !IsLoaded;

	protected bool IsPrerendering =>
		HostEnvironment.Environment.Contains("Prerendering", StringComparison.OrdinalIgnoreCase);

	protected Task LoadedTask => this.taskCompletionSource.Task;

	[Inject]
	protected ILogger<PageBase<T>> Logger { get; set; } = null!;

	protected abstract string? ObjectStoreKey { get; }

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

		await this.semaphore.WaitAsync(cancellationToken);

		try
		{
			// TODO: Polly
			SnapshotResponse<T> response = await HttpClient.GetSnapshotResponse<T>(ObjectStoreUrl, cancellationToken);

			Stopwatch stopwatch = Stopwatch.StartNew();

			// Check manually to avoid additional render cycles
			if (Snapshot is null || string.IsNullOrWhiteSpace(response.ETag) || Snapshot.ETag != response.ETag)
			{
				Snapshot = await response.ToSnapshotAsync(cancellationToken);

				await OnAfterSnapshotLoadedAsync(cancellationToken);
				DeserializeElapsedMilliseconds = (int)stopwatch.ElapsedMilliseconds;
			}
		}
		finally
		{
			this.semaphore.Release();
			this.taskCompletionSource.TrySetResult();
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			AppState.OnAppStateChanged += OnAppStateChanged;
		}
	}

	protected virtual Task OnAfterSnapshotLoadedAsync(CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	protected virtual void OnAppStateChanged(object? sender, AppState e) =>
		LoadAsync().ContinueWith(async _ => await InvokeAsync(StateHasChanged));

	protected override async Task OnParametersSetAsync()
	{
		await base.OnParametersSetAsync();
		await LoadAsync();
	}
}