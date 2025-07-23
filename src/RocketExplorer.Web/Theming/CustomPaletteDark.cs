using MudBlazor;
using MudBlazor.Utilities;

namespace RocketExplorer.Web.Theming;

public class CustomPaletteDark : PaletteDark
{
	public MudColor OnPrimaryContainer { get; set; } = new();

	public MudColor OnSecondaryContainer { get; set; } = new();

	public MudColor OnSurface { get; set; } = new();

	public MudColor OnSurfaceVariant { get; set; } = new();

	public MudColor PrimaryContainer { get; set; } = new();

	public MudColor SecondaryContainer { get; set; } = new();

	public MudColor SurfaceContainer { get; set; } = new();

	public MudColor SurfaceContainerHigh { get; set; } = new();

	public MudColor SurfaceContainerHighest { get; set; } = new();

	public MudColor SurfaceContainerLow { get; set; } = new();

	public MudColor SurfaceContainerLowest { get; set; } = new();

	public MudColor SurfaceVariant { get; set; } = new();
}