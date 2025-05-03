using MudBlazor;
using MudBlazor.Utilities;

namespace RocketExplorer.Web.Theming;

public static class PaletteExtensions
{
	public static MudColor OnPrimary(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.OnPrimary,
			CustomPaletteDark customPaletteDark => customPaletteDark.OnPrimary,
			_ => new MudColor(),
		};

	public static MudColor OnPrimaryContainer(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.OnPrimaryContainer,
			CustomPaletteDark customPaletteDark => customPaletteDark.OnPrimaryContainer,
			_ => new MudColor(),
		};

	public static MudColor PrimaryContainer(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.PrimaryContainer,
			CustomPaletteDark customPaletteDark => customPaletteDark.PrimaryContainer,
			_ => new MudColor(),
		};
}