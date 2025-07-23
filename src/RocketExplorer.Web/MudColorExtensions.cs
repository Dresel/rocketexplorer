using LiveChartsCore.Drawing;
using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView.Painting;
using MudBlazor.Utilities;
using SkiaSharp;

namespace RocketExplorer.Web;

public static class MudColorExtensions
{
	public static LvcColor ToLvcColor(this MudColor mudColor) =>
		new(mudColor.R, mudColor.G, mudColor.B, mudColor.A);

	public static LvcColor ToLvcColor(this uint argb)
	{
		byte a = (byte)((argb & 0xFF000000) >> 24);
		byte r = (byte)((argb & 0x00FF0000) >> 16);
		byte g = (byte)((argb & 0x0000FF00) >> 8);
		byte b = (byte)(argb & 0x000000FF);

		return new LvcColor(r, g, b, a);
	}

	public static uint ToMaterial3(this MudColor mudColor) => (uint)(mudColor.A << 24) | (uint)(mudColor.R << 16) |
		(uint)(mudColor.G << 8) | mudColor.B;

	public static MudColor ToMudColor(this uint argb)
	{
		byte a = (byte)((argb & 0xFF000000) >> 24);
		byte r = (byte)((argb & 0x00FF0000) >> 16);
		byte g = (byte)((argb & 0x0000FF00) >> 8);
		byte b = (byte)(argb & 0x000000FF);

		return new MudColor(r, g, b, a);
	}

	public static MudColor ToMudColor(this string mudColor) =>
		MudColor.Parse(mudColor);

	public static Paint ToPaint(this MudColor mudColor) =>
		new SolidColorPaint(new SKColor(mudColor.R, mudColor.G, mudColor.B, mudColor.A));

	public static SKColor ToSkColor(this MudColor mudColor) =>
		new(mudColor.R, mudColor.G, mudColor.B, mudColor.A);

	public static SKColor ToSkColor(this string mudColor) =>
		MudColor.Parse(mudColor).ToSkColor();
}