using MudBlazor;

namespace RocketExplorer.Web.Theming;

public class ThemeService
{
	private bool systemPreferenceIsDarkMode;

	public event EventHandler<bool>? DarkModeChanged;

	public DarkLightMode CurrentDarkLightMode { get; private set; }

	public Palette CurrentPalette => IsDarkMode ? CurrentTheme.PaletteDark : CurrentTheme.PaletteLight;

	public MudTheme CurrentTheme { get; set; } = new();

	public bool IsDarkMode { get; set; }

	public void CycleDarkLightMode()
	{
		bool wasDarkMode = IsDarkMode;

		switch (CurrentDarkLightMode)
		{
			// Change to Light
			case DarkLightMode.System:
				CurrentDarkLightMode = DarkLightMode.Light;
				IsDarkMode = false;
				break;

			// Change to Dark
			case DarkLightMode.Light:
				CurrentDarkLightMode = DarkLightMode.Dark;
				IsDarkMode = true;
				break;

			// Change to System
			case DarkLightMode.Dark:
				CurrentDarkLightMode = DarkLightMode.System;
				IsDarkMode = this.systemPreferenceIsDarkMode;
				break;

			default:
				throw new ArgumentOutOfRangeException();
		}

		if (wasDarkMode != IsDarkMode)
		{
			OnDarkModeChanged();
		}
	}

	public Task OnSystemPreferenceChanged(bool newValue)
	{
		this.systemPreferenceIsDarkMode = newValue;

		bool wasDarkMode = IsDarkMode;

		if (CurrentDarkLightMode == DarkLightMode.System)
		{
			IsDarkMode = newValue;

			if (wasDarkMode != IsDarkMode)
			{
				OnDarkModeChanged();
			}
		}

		return Task.CompletedTask;
	}

	private void OnDarkModeChanged() => DarkModeChanged?.Invoke(this, IsDarkMode);
}