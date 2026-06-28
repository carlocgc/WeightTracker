using System.Globalization;
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
