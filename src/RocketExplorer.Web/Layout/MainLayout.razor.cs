using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MessagePack;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Shared;
using RocketExplorer.Web.Theming;
using SkiaSharp;

namespace RocketExplorer.Web.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
	private readonly CancellationTokenSource cancellationTokenSourceTimer = new();

	private bool drawerOpen = true;
	private bool isDisposed;
	private MudTheme theme = null!;
	private MudThemeProvider themeProvider = null!;
	private PeriodicTimer? timer;

	[Inject]
	public AppState AppState { get; set; } = null!;

	[Inject]
	public Configuration Configuration { get; set; } = null!;

	[Inject]
	public HttpClient HttpClient { get; set; } = null!;

	[Inject]
	public ThemeService ThemeService { get; set; } = null!;

	[Inject]
	public Web3 Web3 { get; set; } = null!;

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!this.isDisposed)
		{
			this.isDisposed = true;

			if (disposing)
			{
				this.cancellationTokenSourceTimer.Cancel();
				this.cancellationTokenSourceTimer.Dispose();
			}
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			await this.themeProvider.WatchSystemPreference(ThemeService.OnSystemPreferenceChanged);

			await LoadAsync();
			await InvokeAsync(StateHasChanged);

			this.timer = new PeriodicTimer(TimeSpan.FromSeconds(4));
			_ = UpdateBlockInfo(this.cancellationTokenSourceTimer.Token);
		}
	}

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		SetMudTheme();

		ThemeService.CurrentTheme = this.theme;
		ThemeService.DarkModeChanged += (_, _) => { SetLiveChartsTheme(); };

		SetLiveChartsTheme();

		LiveCharts.DefaultSettings.HasGlobalSKTypeface(
			SKTypeface.FromStream(new MemoryStream(await HttpClient.GetByteArrayAsync("fonts/Manrope.ttf"))));
	}

	private Typography CreateTypography()
	{
		string[]? headlineFontFamily = ["Mona Sans", "Helvetica", "Arial", "sans-serif",];

		return new Typography
		{
			Default =
			{
				FontFamily = ["Manrope", "Helvetica", "Arial", "sans-serif",],
			},
			H1 =
			{
				FontFamily = headlineFontFamily,
				FontSize = "clamp(1.5rem, 5vw, 4rem)",
			},
			H2 =
			{
				FontFamily = headlineFontFamily,
				FontSize = "clamp(1.375rem, 4vw, 3rem)",
			},
			H3 =
			{
				FontFamily = headlineFontFamily,
				FontSize = "clamp(1.25rem, 3.5vw, 2.25rem)",
			},
			H4 =
			{
				FontFamily = headlineFontFamily,
				FontSize = "clamp(1.125rem, 3vw, 1.75rem)",
			},
			H5 =
			{
				FontFamily = headlineFontFamily,
				FontSize = "clamp(1rem, 2.5vw, 1.5rem)",
			},
			H6 =
			{
				FontFamily = headlineFontFamily,
				FontSize = "clamp(0.875rem, 2vw, 1.25rem)",
			},
		};
	}

	private async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		Task<BlockWithTransactions>? getBlockTask = Web3.Eth.Blocks
			.GetBlockWithTransactionsByNumber
			.SendRequestAsync(BlockParameter.CreateLatest());
		Task<Stream> getMetadataStreamTask = HttpClient.GetStreamAsync(ObjectStoreMetadataUrl, cancellationToken);

		await Task.WhenAll(getBlockTask, getMetadataStreamTask);

		BlockWithTransactions block = await getBlockTask;
		SnapshotMetadata snapshotMetadata = MessagePackSerializer.Deserialize<SnapshotMetadata>(
			await getMetadataStreamTask, MessagePackSerializerOptions.Standard);

		AppState.Set(block, snapshotMetadata);
	}

	private void SetLiveChartsTheme()
	{
		if (ThemeService.IsDarkMode)
		{
			SetLiveChartsDarkTheme();
		}
		else
		{
			SetLiveChartsLightTheme();
		}
	}

	private async Task UpdateBlockInfo(CancellationToken cancellationToken)
	{
		try
		{
			while (!this.cancellationTokenSourceTimer.IsCancellationRequested &&
					await this.timer!.WaitForNextTickAsync(cancellationToken))
			{
				await LoadAsync(cancellationToken);
				await InvokeAsync(StateHasChanged);
			}
		}
		catch (OperationCanceledException)
		{
			// Ignore cancellation
		}
		finally
		{
			this.timer?.Dispose();
		}
	}
}