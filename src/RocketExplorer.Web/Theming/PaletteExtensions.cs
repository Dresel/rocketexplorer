using MaterialColorUtilities.Schemes;
using MaterialColorUtilities.Utils;
using MudBlazor;
using MudBlazor.Utilities;

namespace RocketExplorer.Web.Theming;

public static class PaletteExtensions
{
	public static MudColor OnPrimaryContainer(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.OnPrimaryContainer,
			CustomPaletteDark customPaletteDark => customPaletteDark.OnPrimaryContainer,
			_ => new MudColor(),
		};

	public static MudColor OnSecondaryContainer(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.OnSecondaryContainer,
			CustomPaletteDark customPaletteDark => customPaletteDark.OnSecondaryContainer,
			_ => new MudColor(),
		};

	public static MudColor OnSurface(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.OnSurface,
			CustomPaletteDark customPaletteDark => customPaletteDark.OnSurface,
			_ => new MudColor(),
		};

	public static MudColor OnSurfaceVariant(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.OnSurfaceVariant,
			CustomPaletteDark customPaletteDark => customPaletteDark.OnSurfaceVariant,
			_ => new MudColor(),
		};

	public static MudColor PrimaryContainer(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.PrimaryContainer,
			CustomPaletteDark customPaletteDark => customPaletteDark.PrimaryContainer,
			_ => new MudColor(),
		};

	public static MudColor SecondaryContainer(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SecondaryContainer,
			CustomPaletteDark customPaletteDark => customPaletteDark.SecondaryContainer,
			_ => new MudColor(),
		};

	public static void SetOnPrimaryContainer(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.OnPrimaryContainer = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.OnPrimaryContainer = color;
				break;
		}
	}

	public static void SetOnSecondaryContainer(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.OnSecondaryContainer = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.OnSecondaryContainer = color;
				break;
		}
	}

	public static void SetOnSurface(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.OnSurface = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.OnSurface = color;
				break;
		}
	}

	public static void SetOnSurfaceVariant(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.OnSurfaceVariant = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.OnSurfaceVariant = color;
				break;
		}
	}

	public static void SetPrimaryContainer(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.PrimaryContainer = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.PrimaryContainer = color;
				break;
		}
	}

	public static void SetSecondaryContainer(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SecondaryContainer = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SecondaryContainer = color;
				break;
		}
	}

	public static void SetSurfaceBright(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SurfaceBright = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SurfaceBright = color;
				break;
		}
	}

	public static void SetSurfaceContainer(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SurfaceContainer = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SurfaceContainer = color;
				break;
		}
	}

	public static void SetSurfaceContainerHigh(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SurfaceContainerHigh = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SurfaceContainerHigh = color;
				break;
		}
	}

	public static void SetSurfaceContainerHighest(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SurfaceContainerHighest = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SurfaceContainerHighest = color;
				break;
		}
	}

	public static void SetSurfaceContainerLow(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SurfaceContainerLow = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SurfaceContainerLow = color;
				break;
		}
	}

	public static void SetSurfaceContainerLowest(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SurfaceContainerLowest = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SurfaceContainerLowest = color;
				break;
		}
	}

	public static void SetSurfaceDim(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SurfaceDim = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SurfaceDim = color;
				break;
		}
	}

	public static void SetSurfaceVariant(this Palette palette, MudColor color)
	{
		switch (palette)
		{
			case CustomPaletteLight customPaletteLight:
				customPaletteLight.SurfaceVariant = color;
				break;
			case CustomPaletteDark customPaletteDark:
				customPaletteDark.SurfaceVariant = color;
				break;
		}
	}

	public static MudColor SurfaceBright(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SurfaceBright,
			CustomPaletteDark customPaletteDark => customPaletteDark.SurfaceBright,
			_ => new MudColor(),
		};

	public static MudColor SurfaceContainer(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SurfaceContainer,
			CustomPaletteDark customPaletteDark => customPaletteDark.SurfaceContainer,
			_ => new MudColor(),
		};

	public static MudColor SurfaceContainerHigh(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SurfaceContainerHigh,
			CustomPaletteDark customPaletteDark => customPaletteDark.SurfaceContainerHigh,
			_ => new MudColor(),
		};

	public static MudColor SurfaceContainerHighest(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SurfaceContainerHighest,
			CustomPaletteDark customPaletteDark => customPaletteDark.SurfaceContainerHighest,
			_ => new MudColor(),
		};

	public static MudColor SurfaceContainerLow(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SurfaceContainerLow,
			CustomPaletteDark customPaletteDark => customPaletteDark.SurfaceContainerLow,
			_ => new MudColor(),
		};

	public static MudColor SurfaceContainerLowest(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SurfaceContainerLowest,
			CustomPaletteDark customPaletteDark => customPaletteDark.SurfaceContainerLowest,
			_ => new MudColor(),
		};

	public static MudColor SurfaceDim(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SurfaceDim,
			CustomPaletteDark customPaletteDark => customPaletteDark.SurfaceDim,
			_ => new MudColor(),
		};

	public static MudColor SurfaceVariant(this Palette palette) =>
		palette switch
		{
			CustomPaletteLight customPaletteLight => customPaletteLight.SurfaceVariant,
			CustomPaletteDark customPaletteDark => customPaletteDark.SurfaceVariant,
			_ => new MudColor(),
		};

	public static uint ToHover(this Scheme<uint> scheme, Func<Scheme<uint>, uint> colorFunc) =>
		colorFunc(scheme).Add(scheme.OnSurface, 0.08);

	public static MudColor ToHover(this Palette palette, Func<Palette, MudColor> colorFunc) =>
		colorFunc(palette).ToMaterial3().Add(palette.OnSurface().ToMaterial3(), .08).ToMudColor();
}