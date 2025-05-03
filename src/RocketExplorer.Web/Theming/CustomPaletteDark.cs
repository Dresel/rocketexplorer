using MudBlazor;
using MudBlazor.Utilities;

namespace RocketExplorer.Web.Theming;

public class CustomPaletteDark : PaletteDark
{
	public MudColor OnPrimary { get; set; } = new();

	public MudColor OnPrimaryContainer { get; set; } = new();

	public MudColor PrimaryContainer { get; set; } = new();
}