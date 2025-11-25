using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using RocketExplorer.Web.Theming;
using SkiaSharp;

namespace RocketExplorer.Web.Layout;

public partial class MainLayout : LayoutComponentBase
{
	private bool drawerOpen = true;
	private MudTheme theme = null!;
	private MudThemeProvider themeProvider = null!;

	[Inject]
	public Configuration Configuration { get; set; } = null!;

	[Inject]
	public HttpClient HttpClient { get; set; } = null!;

	[Inject]
	public ThemeService ThemeService { get; set; } = null!;

	[Inject]
	public IWebAssemblyHostEnvironment HostEnvironment { get; set; } = null!;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			await this.themeProvider!.WatchSystemPreference(ThemeService.OnSystemPreferenceChanged);
		}
	}

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		SetMudTheme();

		ThemeService.CurrentTheme = this.theme;
		ThemeService.DarkModeChanged += (_, _) => { SetLiveChartsTheme(); };

		SetLiveChartsTheme();

		if (!HostEnvironment.Environment.Contains("Prerendering", StringComparison.OrdinalIgnoreCase))
		{
			LiveCharts.DefaultSettings.HasTextSettings(
				new()
				{
					DefaultTypeface =
						SKTypeface.FromStream(new MemoryStream(await HttpClient.GetByteArrayAsync("fonts/Manrope.ttf"))),
 
				});
		}
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
}