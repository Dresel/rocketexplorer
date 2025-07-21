using MudBlazor;
using MudBlazor.Utilities;

namespace RocketExplorer.Web.Theming;

public class CustomPaletteLight : PaletteLight
{
	public MudColor OnPrimary { get; set; } = new();

	public MudColor OnPrimaryContainer { get; set; } = new();

	public MudColor PrimaryContainer { get; set; } = new();

	public MudColor Surface3 { get; set; } = new();
}