using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Web.Pages;

public sealed record DashboardHistoryRow(DateOnly EntryDate, decimal WeightKg);

public sealed record DashboardCalendarDay(
    DateOnly EntryDate,
    decimal? WeightKg,
    bool IsToday,
    bool IsFuture)
{
    public bool HasEntry => WeightKg.HasValue;
}

public sealed class IndexModel(
    SettingsService settingsService,
    WeightEntryService entryService,
    WeightDataService weightDataService,
    MetricsService metricsService,
    ILocalDateProvider localDateProvider) : PageModel
{
    private const int ChartDayCount = 180;
    private const int RecentHistoryCount = 7;

    [BindProperty]
    public DateOnly EntryDate { get; set; }

    [BindProperty]
    public decimal? Weight { get; set; }

    [BindProperty]
    public decimal? GoalWeight { get; set; }

    [BindProperty]
    public IFormFile? ImportFile { get; set; }

    [BindProperty]
    public string? DeleteAllConfirmation { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty(SupportsGet = true, Name = "month")]
    public string? CalendarMonth { get; set; }

    public string DisplayUnit { get; private set; } = "kg";

    public string Theme { get; private set; } = "dark";

    public DateOnly Today { get; private set; }

    public DateOnly VisibleMonth { get; private set; }

    public decimal? TodayWeightKg { get; private set; }

    public IReadOnlyList<DashboardHistoryRow> RecentHistory { get; private set; } = [];

    public IReadOnlyList<DashboardCalendarDay> CalendarDays { get; private set; } = [];

    public int CalendarLeadingBlankCount { get; private set; }

    public string PreviousCalendarMonthQuery { get; private set; } = string.Empty;

    public string? NextCalendarMonthQuery { get; private set; }

    public MetricsSummary Summary { get; private set; } = new(null, null, null, null, null, null, null, null, null, null);

    public GoalProgressInsights ProgressInsights { get; private set; } = new(
        GoalDirection.None,
        DirectionalStatus.Unknown,
        DirectionalStatus.Unknown,
        DirectionalStatus.Unknown,
        new GoalForecast(GoalForecastStatus.NoGoal, null, null, null, null),
        []);

    public ChartSeries Chart { get; private set; } = new([], [], [], null);

    public ChartSeries LongRangeChart { get; private set; } = new([], [], [], null);

    public int EntryCount { get; private set; }

    public bool GoalDialogOpen { get; private set; }

    public string GoalDialogOpenAttribute => GoalDialogOpen ? "true" : "false";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Weight is null or <= 0)
        {
            ModelState.AddModelError(nameof(Weight), "Enter a weight greater than zero.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            await entryService.SaveAsync(EntryDate, Weight.Value, cancellationToken);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            ModelState.AddModelError(nameof(EntryDate), exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await entryService.DeletePastAsync(EntryDate, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty, "Only past entries can be deleted.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostGoalAsync(CancellationToken cancellationToken)
    {
        if (GoalWeight is null or <= 0)
        {
            ModelState.AddModelError(nameof(GoalWeight), "Enter a goal greater than zero.");
            GoalDialogOpen = true;
            await LoadAsync(cancellationToken);
            return Page();
        }

        var settings = await settingsService.GetAsync(cancellationToken);
        var goalWeightKg = decimal.Round(WeightConversionService.ToKilograms(GoalWeight.Value, settings.DisplayUnit), 3);
        await settingsService.UpdateAsync(
            settings.DisplayUnit,
            goalWeightKg,
            settings.WeekStartsOn,
            settings.TimeZoneId,
            settings.Theme,
            cancellationToken);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearGoalAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        await settingsService.UpdateAsync(
            settings.DisplayUnit,
            null,
            settings.WeekStartsOn,
            settings.TimeZoneId,
            settings.Theme,
            cancellationToken);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetExportCsvAsync(CancellationToken cancellationToken)
    {
        var today = await localDateProvider.GetTodayAsync(cancellationToken);
        var csv = await weightDataService.ExportCsvAsync(cancellationToken);
        var fileName = $"weighttracker-weights-{today:yyyyMMdd}.csv";

        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    public async Task<IActionResult> OnPostImportCsvAsync(CancellationToken cancellationToken)
    {
        if (ImportFile is null || ImportFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Choose a CSV file to import.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        using var reader = new StreamReader(ImportFile.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var csv = await reader.ReadToEndAsync(cancellationToken);
        var result = await weightDataService.ImportCsvAsync(csv, cancellationToken);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadAsync(cancellationToken);
            return Page();
        }

        StatusMessage = $"Imported {result.InsertedCount + result.UpdatedCount} entries.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAllWeightsAsync(CancellationToken cancellationToken)
    {
        var result = await weightDataService.DeleteAllWeightsAsync(DeleteAllConfirmation, cancellationToken);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadAsync(cancellationToken);
            return Page();
        }

        StatusMessage = $"Deleted {result.DeletedCount} entries.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        DisplayUnit = settings.DisplayUnit;
        Theme = settings.Theme;
        Today = await localDateProvider.GetTodayAsync(cancellationToken);
        VisibleMonth = ResolveVisibleMonth(CalendarMonth, Today);

        var visibleMonthStart = new DateOnly(VisibleMonth.Year, VisibleMonth.Month, 1);
        var chartStart = Today.AddDays(-ChartDayCount);

        var entries = await entryService.GetRangeAsync(DateOnly.MinValue, Today, cancellationToken);
        var compactChartEntries = entries
            .Where(entry => entry.EntryDate >= chartStart)
            .ToList();
        var entriesByDate = entries.ToDictionary(entry => entry.EntryDate);

        TodayWeightKg = entriesByDate.TryGetValue(Today, out var todayEntry)
            ? todayEntry.WeightKg
            : null;

        RecentHistory = entries
            .Where(entry => entry.EntryDate <= Today)
            .OrderByDescending(entry => entry.EntryDate)
            .Take(RecentHistoryCount)
            .Select(entry => new DashboardHistoryRow(entry.EntryDate, entry.WeightKg))
            .ToList();

        CalendarDays = BuildCalendarDays(visibleMonthStart, entriesByDate);
        CalendarLeadingBlankCount = ((int)visibleMonthStart.DayOfWeek + 6) % 7;
        PreviousCalendarMonthQuery = visibleMonthStart.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var currentMonthStart = new DateOnly(Today.Year, Today.Month, 1);
        var nextMonthStart = visibleMonthStart.AddMonths(1);
        NextCalendarMonthQuery = nextMonthStart <= currentMonthStart
            ? nextMonthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : null;

        Summary = metricsService.BuildSummary(entries, Today, settings.WeekStartsOn, settings.GoalWeightKg);
        ProgressInsights = metricsService.BuildMotivationalInsights(entries, Today, settings.WeekStartsOn, settings.GoalWeightKg);
        Chart = metricsService.BuildChartSeries(compactChartEntries, settings.WeekStartsOn, settings.GoalWeightKg);
        LongRangeChart = metricsService.BuildChartSeries(entries, settings.WeekStartsOn, settings.GoalWeightKg);
        EntryCount = entries.Count;
        if (!GoalDialogOpen)
        {
            GoalWeight = Summary.GoalWeightKg.HasValue
                ? decimal.Round(WeightConversionService.FromKilograms(Summary.GoalWeightKg.Value, DisplayUnit), 1)
                : null;
        }
    }

    public string CalendarMonthLabel => VisibleMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public string EntryDateIso(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public string FormatHistoryDate(DateOnly value)
    {
        return value.ToString("dddd, dd MMM", CultureInfo.InvariantCulture);
    }

    public string FormatWeight(decimal? valueKg)
    {
        return valueKg is null
            ? "-"
            : $"{WeightConversionService.FromKilograms(valueKg.Value, DisplayUnit):0.0} {DisplayUnit}";
    }

    public string FormatSignedWeight(decimal? valueKg)
    {
        if (valueKg is null)
        {
            return "-";
        }

        var display = valueKg.Value == 0
            ? 0
            : WeightConversionService.FromKilograms(Math.Abs(valueKg.Value), DisplayUnit) * Math.Sign(valueKg.Value);
        return $"{display:+0.0;-0.0;0.0} {DisplayUnit}";
    }

    public string DirectionStatusClass(DirectionalStatus status)
    {
        return status switch
        {
            DirectionalStatus.TowardGoal => "metric-status--toward",
            DirectionalStatus.AwayFromGoal => "metric-status--away",
            DirectionalStatus.Neutral => "metric-status--neutral",
            _ => "metric-status--unknown"
        };
    }

    public string DirectionStatusLabel(DirectionalStatus status)
    {
        return status switch
        {
            DirectionalStatus.TowardGoal => "toward goal",
            DirectionalStatus.AwayFromGoal => "away from goal",
            DirectionalStatus.Neutral => "neutral",
            _ => "unknown"
        };
    }

    public string DirectionArrow(decimal? changeKg, DirectionalStatus status)
    {
        if (status == DirectionalStatus.Unknown || !changeKg.HasValue)
        {
            return string.Empty;
        }

        if (Math.Abs(changeKg.Value) < 0.05m)
        {
            return "→";
        }

        return changeKg.Value < 0 ? "↓" : "↑";
    }

    public string FormatForecastValue()
    {
        return ProgressInsights.Forecast.Status switch
        {
            GoalForecastStatus.Estimated when ProgressInsights.Forecast.EstimatedDate.HasValue
                => $"Estimated {ProgressInsights.Forecast.EstimatedDate.Value.ToString("MMM yyyy", CultureInfo.InvariantCulture)}",
            GoalForecastStatus.AtGoal => "At goal",
            GoalForecastStatus.MovingAwayFromGoal => "Moving away from goal",
            GoalForecastStatus.PaceTooFlat => "Pace too flat to project",
            GoalForecastStatus.NeedMoreData => "Need more recent data",
            GoalForecastStatus.NoLatestWeight => "Waiting for first weight",
            _ => "Set a goal to unlock forecast"
        };
    }

    public string FormatForecastDetail()
    {
        return ProgressInsights.Forecast.Status switch
        {
            GoalForecastStatus.Estimated when ProgressInsights.Forecast.SourceWindow is not null
                => $"Based on {ProgressInsights.Forecast.SourceWindow} pace",
            GoalForecastStatus.AtGoal => "Maintenance target reached",
            GoalForecastStatus.MovingAwayFromGoal => "Recent pace is not closing the gap",
            GoalForecastStatus.PaceTooFlat => "Recent movement is too small",
            GoalForecastStatus.NeedMoreData => "Add more entries to estimate pace",
            GoalForecastStatus.NoLatestWeight => "Add your first entry",
            _ => "Goal-aware estimates need a target"
        };
    }

    public string FormatRecordEmptyState()
    {
        return Summary.GoalWeightKg.HasValue
            ? "No goal-direction record yet."
            : "Set a goal to unlock goal-direction records.";
    }

    public string FormatRecordLabel(GoalProgressRecord record)
    {
        return record.WindowDays.HasValue
            ? $"Best {record.WindowDays.Value}-day progress"
            : "Best all-time progress";
    }

    public string FormatRecordRange(GoalProgressRecord record)
    {
        return $"{record.StartDate.ToString("dd MMM", CultureInfo.InvariantCulture)} to {record.EndDate.ToString("dd MMM", CultureInfo.InvariantCulture)}";
    }

    public string FormatGoalDistance()
    {
        return Summary.LatestWeightKg.HasValue && Summary.GoalWeightKg.HasValue
            ? FormatSignedWeight(Summary.LatestWeightKg.Value - Summary.GoalWeightKg.Value)
            : FormatSignedWeight(Summary.ThirtyDayChangeKg);
    }

    public bool HasGoal => Summary.GoalWeightKg.HasValue;

    public string GoalActionLabel => HasGoal ? "Edit goal" : "Set goal";

    public string FormatGoalPanelDetail()
    {
        if (!Summary.GoalWeightKg.HasValue)
        {
            return "Set a target weight";
        }

        if (!Summary.LatestWeightKg.HasValue)
        {
            return "Waiting for your first weight";
        }

        return FormatSignedWeight(Summary.LatestWeightKg.Value - Summary.GoalWeightKg.Value);
    }

    public string GoalInputValue()
    {
        if (GoalDialogOpen && GoalWeight.HasValue)
        {
            return GoalWeight.Value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        return InputWeightValue(Summary.GoalWeightKg);
    }

    public string InputValue(DashboardCalendarDay day)
    {
        return InputWeightValue(day.WeightKg);
    }

    public string InputWeightValue(decimal? valueKg)
    {
        return valueKg is null
            ? string.Empty
            : decimal.Round(WeightConversionService.FromKilograms(valueKg.Value, DisplayUnit), 1).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private IReadOnlyList<DashboardCalendarDay> BuildCalendarDays(
        DateOnly visibleMonthStart,
        IReadOnlyDictionary<DateOnly, WeightEntry> entriesByDate)
    {
        return Enumerable.Range(0, DateTime.DaysInMonth(visibleMonthStart.Year, visibleMonthStart.Month))
            .Select(offset => visibleMonthStart.AddDays(offset))
            .Select(date => new DashboardCalendarDay(
                date,
                entriesByDate.TryGetValue(date, out var entry) ? entry.WeightKg : null,
                date == Today,
                date > Today))
            .ToList();
    }

    private static DateOnly ResolveVisibleMonth(string? month, DateOnly today)
    {
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        if (string.IsNullOrWhiteSpace(month))
        {
            return currentMonth;
        }

        if (!DateTime.TryParseExact(
            month,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            return currentMonth;
        }

        var requested = new DateOnly(parsed.Year, parsed.Month, 1);
        return requested > currentMonth ? currentMonth : requested;
    }
}
