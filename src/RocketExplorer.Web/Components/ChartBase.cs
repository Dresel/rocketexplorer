using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.State;
using MudBlazor.State.Builder;
using RocketExplorer.Web.Theming;

namespace RocketExplorer.Web.Components;

public class ChartBase : MudComponentBase
{
	public ChartBase()
	{
		using IParameterRegistrationBuilderScope registerScope = CreateRegisterScope();

		registerScope.RegisterParameter<SortedList<DateOnly, int>[]?>(nameof(Data))
			.WithParameter(() => Data)
			.WithChangeHandler(OnParametersChanged);

		registerScope.RegisterParameter<Func<int, int>[]?>(nameof(DataTransform))
			.WithParameter(() => DataTransform)
			.WithChangeHandler(OnParametersChanged);

		//aggregationParameterState = registerScope.RegisterParameter<ChartAggregation>(nameof(Aggregation))
		//	.WithParameter(() => Aggregation)
		//	.WithChangeHandler(OnParametersChanged);

		registerScope.RegisterParameter<double?>(nameof(MinLimit))
			.WithParameter(() => MinLimit)
			.WithChangeHandler(OnParametersChanged);

		registerScope.RegisterParameter<string?>(nameof(Title))
			.WithParameter(() => Title)
			.WithChangeHandler(OnParametersChanged);

		registerScope.RegisterParameter<string?>(nameof(YAxesName))
			.WithParameter(() => YAxesName)
			.WithChangeHandler(OnParametersChanged);
	}

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

	protected ChartAggregation Aggregation { get; private set; }

	protected bool Expanded { get; set; }

	protected Guid Key { get; set; } = Guid.NewGuid();

	[Inject]
	protected ThemeService ThemeService { get; set; } = null!;

	protected List<TrackedParameter> TrackedParameters { get; set; } = [];

	protected ICartesianAxis[] XAxes { get; private set; } = [];

	// TODO: Do not automatically
	protected ICartesianAxis[] YAxes { get; set; } = [];

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

	protected async Task SetAggregation(ChartAggregation value)
	{
		Aggregation = value;
		await SetSeriesAsync();
		StateHasChanged();
	}

	protected void SetExpanded(bool value)
	{
		Expanded = value;
		_ = SetSeriesAsync();
	}

	protected virtual Task SetSeriesAsync() => Task.CompletedTask;

	private ICartesianAxis[] CreateXAxes()
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
				DateTime[] customSeparators = Enumerable.Range(2020, DateTime.Now.Year - 2020 + 1)
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
				target = DateOnly.FromDateTime(DateTime.Now).AddMonths(Expanded ? -36 : -12);
				dateTimeAxis.MinLimit = new DateTime(target.Year, target.Month, 1).Ticks;
				dateTimeAxis.MaxLimit = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).Ticks;
				break;

			case ChartAggregation.Daily:
				target = DateOnly.FromDateTime(DateTime.Now).AddDays(Expanded ? -42 : -14);
				dateTimeAxis.MinLimit = new DateTime(target.Year, target.Month, target.Day).AddDays(-0.5).Ticks;
				dateTimeAxis.MaxLimit = DateTime.Now.Date.AddDays(0.5).Ticks;
				break;
		}

		return
		[
			dateTimeAxis,
		];
	}

	private async Task OnParametersChanged()
	{
		UpdateXAxes();
		await SetSeriesAsync();
	}

	private void UpdateXAxes()
	{
		ICartesianAxis[] cartesianAxisArray = CreateXAxes();

		if (XAxes.Length > 0)
		{
			// Check for equality
			bool customSeparatorsEqual = XAxes[0].CustomSeparators is { } a &&
				cartesianAxisArray[0].CustomSeparators is { } b
					? a.SequenceEqual(b)
					: ReferenceEquals(XAxes[0].CustomSeparators, cartesianAxisArray[0].CustomSeparators);

			if (Math.Abs(XAxes[0].MinLimit!.Value - cartesianAxisArray[0].MinLimit!.Value) < double.Epsilon &&
				Math.Abs(XAxes[0].MaxLimit!.Value - cartesianAxisArray[0].MaxLimit!.Value) < double.Epsilon &&
				customSeparatorsEqual)
			{
				return;
			}
		}

		XAxes = cartesianAxisArray;
	}

	private void UpdateYAxes() =>
		YAxes =
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
}