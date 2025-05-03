using LiveChartsCore.Drawing;
using MudBlazor.Utilities;
using SkiaSharp;

namespace RocketExplorer.Web;

public static class MudColorExtensions
{
	public static LvcColor ToLvcColor(this MudColor mudColor) =>
		new(mudColor.R, mudColor.G, mudColor.B, mudColor.A);

	public static MudColor ToMudColor(this string mudColor) =>
		MudColor.Parse(mudColor);

	public static SKColor ToSkColor(this MudColor mudColor) =>
		new(mudColor.R, mudColor.G, mudColor.B, mudColor.A);

	public static SKColor ToSkColor(this string mudColor) =>
		MudColor.Parse(mudColor).ToSkColor();
}