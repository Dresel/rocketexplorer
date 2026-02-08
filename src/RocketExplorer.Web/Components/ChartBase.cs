using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using Microsoft.AspNetCore.Components;
using RocketExplorer.Web.Theming;

namespace RocketExplorer.Web.Components;

public class ChartBase : ComponentBase
{
	[Parameter]
	public SortedList<DateOnly, int>[]? Data { get; set; }

	[Parameter]
	public Func<int, int>[]? DataTransform { get; set; }

	[Parameter]
	public double? MinLimit { get; set; } = -0.9;

	[Parameter]
	public ISeries[]? Series { get; set; } = [];

	[Parameter]
	public string? Title { get; set; }

	[Parameter]
	public string? YAxesName { get; set; }

	protected ChartAggregation Aggregation { get; set; } = ChartAggregation.Monthly;

	protected bool Expanded { get; set; }

	protected Guid Key { get; set; } = Guid.NewGuid();

	[Inject]
	protected ThemeService ThemeService { get; set; } = null!;

	protected List<TrackedParameter> TrackedParameters { get; set; } = [];

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

			DateTimeAxis dateTimeAxis = new(unit, date => date.ToString(dateTimeFormat))
			{
				TextSize = 13,
				NameTextSize = 14,
			};

			DateOnly target;

			switch (Aggregation)
			{
				case ChartAggregation.Yearly:
					int minYear = Data?.SelectMany(x => x.Keys).Min(x => x.Year - 1) ?? 2020;
					DateTime[] customSeparators = Enumerable.Range(minYear, DateTime.Now.Year - minYear + 1)
						.Select(y => new DateTime(y, 7, 1))
						.ToArray();
					dateTimeAxis.CustomSeparators = customSeparators.Select(x => (double)x.Ticks).ToArray();

					if (GetType() == typeof(ChartDelta) && Data?.Sum(x => x.Count) == 0)
					{
						dateTimeAxis.MinLimit = customSeparators.Last().AddMonths(-6).Ticks;
						dateTimeAxis.MaxLimit = customSeparators.Last().AddMonths(6).Ticks;
					}

					break;

				case ChartAggregation.Monthly:
					target = DateOnly.FromDateTime(DateTime.Today).AddMonths(Expanded ? -36 : -12);
					dateTimeAxis.MinLimit = new DateTime(target.Year, target.Month, 1).Ticks;
					dateTimeAxis.MaxLimit = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).Ticks;
					break;

				case ChartAggregation.Daily:
					target = DateOnly.FromDateTime(DateTime.Now).AddDays(Expanded ? -42 : -14);
					dateTimeAxis.MinLimit = new DateTime(target.Year, target.Month, target.Day).AddDays(-0.5).Ticks;
					dateTimeAxis.MaxLimit = DateTime.Today.AddDays(0.5).Ticks;
					break;
			}

			return
			[
				dateTimeAxis,
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
			MinLimit = MinLimit,
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

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		TrackedParameters =
		[
			new TrackedParameter(() => Data),
			new TrackedParameter(() => DataTransform),
			new TrackedParameter(() => MinLimit),
			new TrackedParameter(() => Series),
			new TrackedParameter(() => Title),
			new TrackedParameter(() => YAxesName),
		];
	}

	protected override async Task OnParametersSetAsync()
	{
		await base.OnParametersSetAsync();

		bool shouldSetSeries = false;

		foreach (TrackedParameter trackedParameter in TrackedParameters)
		{
			shouldSetSeries |= trackedParameter.Update();
		}

		if (shouldSetSeries)
		{
			SetSeries();
		}
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