using System.Reflection;
using System.Text;
using MudBlazor;
using MudBlazor.State;

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

		theme.AppendLine($"--mark-color: {palette.Tertiary};");

		theme.AppendLine($"--mark-color: {palette.Tertiary};");
		theme.AppendLine($"--mark-background-color: {palette.TertiaryContrastText};");

		theme.AppendLine($"--mark-color: {palette.Tertiary};");

		theme.AppendLine($"--mud-palette-on-primary: {palette.OnPrimary()};");
		theme.AppendLine($"--mud-palette-primary-container: {palette.PrimaryContainer()};");
		theme.AppendLine($"--mud-palette-on-primary-container: {palette.OnPrimaryContainer()};");

		theme.AppendLine($"--mud-palette-surface-3: {palette.Surface3()};");
	}
}