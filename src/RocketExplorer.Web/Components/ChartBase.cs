using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using Microsoft.AspNetCore.Components;
using RocketExplorer.Web.Theming;

namespace RocketExplorer.Web.Components;

public class ChartBase : ComponentBase
{
	private SortedList<DateOnly, int>? previousData;
	private string? previousTitle;
	private string? previousYAxesName;

	[Parameter]
	public SortedList<DateOnly, int>? Data { get; set; } = [];

	[Parameter]
	public string? Title { get; set; }

	[Parameter]
	public string? YAxesName { get; set; }

	protected ChartAggregation Aggregation { get; set; } = ChartAggregation.Monthly;

	protected bool Expanded { get; set; }

	protected Guid Key { get; set; } = Guid.NewGuid();

	protected ISeries[] Series { get; set; } = [];

	[Inject]
	protected ThemeService ThemeService { get; set; } = null!;

	protected virtual ICartesianAxis[] XAxes
	{
		get
		{
			TimeSpan unit = Aggregation switch
			{
				ChartAggregation.Daily => TimeSpan.FromDays(1),
				ChartAggregation.Monthly => TimeSpan.FromDays(30),
				ChartAggregation.Yearly => TimeSpan.FromDays(365),
				_ => throw new ArgumentOutOfRangeException(nameof(Aggregation)),
			};

			string dateTimeFormat = Aggregation switch
			{
				ChartAggregation.Daily => "dd.MM.yyyy",
				ChartAggregation.Monthly => "MM.yyyy",
				ChartAggregation.Yearly => "yyyy",
				_ => throw new ArgumentOutOfRangeException(nameof(Aggregation)),
			};

			return
			[
				new DateTimeAxis(unit, date => date.ToString(dateTimeFormat))
				{
					TextSize = 13,
					NameTextSize = 14,
				},
			];
		}
	}

	protected virtual ICartesianAxis[] YAxes =>
	[
		new Axis
		{
			Name = YAxesName ?? string.Empty,
			MinStep = 1,
			TextSize = 13,
			NameTextSize = 14,
		},
	];

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			ThemeService.DarkModeChanged += (_, darkMode) =>
			{
				Key = Guid.NewGuid();
				StateHasChanged();
			};
		}
	}

	protected override async Task OnParametersSetAsync()
	{
		await base.OnParametersSetAsync();

		if (Data != this.previousData || Title != this.previousTitle || YAxesName != this.previousYAxesName)
		{
			SetSeries();
		}

		this.previousData = Data;
		this.previousTitle = Title;
		this.previousYAxesName = YAxesName;
	}

	protected void SetAggregation(ChartAggregation value)
	{
		Aggregation = value;
		SetSeries();
	}

	protected void SetExpanded(bool value)
	{
		Expanded = value;
		SetSeries();
	}

	protected virtual void SetSeries()
	{
	}
}