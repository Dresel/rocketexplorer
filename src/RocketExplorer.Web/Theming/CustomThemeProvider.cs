using System.Reflection;
using System.Text;
using MudBlazor;
using MudBlazor.State;
using MudBlazor.Utilities;

namespace RocketExplorer.Web.Theming;

public class CustomThemeProvider : MudThemeProvider
{
	protected override void GenerateTheme(StringBuilder theme)
	{
		base.GenerateTheme(theme);

		// Get private field _theme
		FieldInfo? themeField = typeof(MudThemeProvider).GetField(
			"_theme", BindingFlags.NonPublic | BindingFlags.Instance);
		MudTheme? mudTheme = (MudTheme?)themeField?.GetValue(this);

		if (mudTheme == null)
		{
			return;
		}

		// Get private field _isDarkModeState
		FieldInfo? isDarkModeStateField = typeof(MudThemeProvider).GetField(
			"_isDarkModeState", BindingFlags.NonPublic | BindingFlags.Instance);
		ParameterState<bool> isDarkModeState = (ParameterState<bool>?)isDarkModeStateField?.GetValue(this) ??
			throw new InvalidOperationException();

		Palette palette = isDarkModeState.Value ? mudTheme.PaletteDark : mudTheme.PaletteLight;

		theme.AppendLine($"--mud-palette-primary-hover: {palette.ToHover(x => new MudColor(255, 255, 255, 0))};");
		theme.AppendLine($"--mud-palette-surface-hover: {palette.ToHover(x => x.Surface)};");

		theme.AppendLine($"--mark-color: {palette.TertiaryContrastText};");
		theme.AppendLine($"--mark-background-color: {palette.Tertiary};");

		theme.AppendLine($"--mud-palette-primary-container: {palette.PrimaryContainer()};");
		theme.AppendLine($"--mud-palette-on-primary-container: {palette.OnPrimaryContainer()};");
		theme.AppendLine($"--mud-palette-on-primary-container-hover: {palette.ToHover(x => x.PrimaryContainer())};");

		theme.AppendLine($"--mud-palette-secondary-container: {palette.SecondaryContainer()};");
		theme.AppendLine($"--mud-palette-on-secondary-container: {palette.OnSecondaryContainer()};");
		theme.AppendLine($"--mud-palette-secondary-container-hover: {palette.ToHover(x => x.SecondaryContainer())};");

		theme.AppendLine($"--mud-palette-surface-variant: {palette.SurfaceVariant()};");
		theme.AppendLine($"--mud-palette-on-surface-variant: {palette.OnSurfaceVariant()};");
		theme.AppendLine($"--mud-palette-surface-variant-hover: {palette.ToHover(x => x.SurfaceVariant())};");

		theme.AppendLine($"--mud-palette-surface-container-lowest: {palette.SurfaceContainerLowest()};");
		theme.AppendLine($"--mud-palette-surface-container-low: {palette.SurfaceContainerLow()};");
		theme.AppendLine($"--mud-palette-surface-container: {palette.SurfaceContainer()};");
		theme.AppendLine($"--mud-palette-surface-container-high: {palette.SurfaceContainerHigh()};");
		theme.AppendLine($"--mud-palette-surface-container-highest: {palette.SurfaceContainerHighest()};");
	}
}