using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Web.Pages;

public sealed record DashboardDateCard(
    DateOnly EntryDate,
    decimal? WeightKg,
    bool IsToday,
    bool CanDelete)
{
    public bool HasEntry => WeightKg.HasValue;
}

public sealed class IndexModel(
    SettingsService settingsService,
    WeightEntryService entryService,
    MetricsService metricsService,
    ILocalDateProvider localDateProvider) : PageModel
{
    private const int CardCount = 14;

    [BindProperty]
    public DateOnly EntryDate { get; set; }

    [BindProperty]
    public decimal? Weight { get; set; }

    public string DisplayUnit { get; private set; } = "kg";

    public string Theme { get; private set; } = "dark";

    public DateOnly Today { get; private set; }

    public IReadOnlyList<DashboardDateCard> Cards { get; private set; } = [];

    public MetricsSummary Summary { get; private set; } = new(null, null, null, null, null, null, null, null, null, null);

    public ChartSeries Chart { get; private set; } = new([], [], [], null);

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

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        DisplayUnit = settings.DisplayUnit;
        Theme = settings.Theme;
        Today = await localDateProvider.GetTodayAsync(cancellationToken);

        var entries = await entryService.GetRangeAsync(Today.AddDays(-180), Today, cancellationToken);
        var entriesByDate = entries.ToDictionary(entry => entry.EntryDate);
        Cards = Enumerable.Range(0, CardCount)
            .Select(offset => Today.AddDays(-offset))
            .Select(date => new DashboardDateCard(
                date,
                entriesByDate.TryGetValue(date, out var entry) ? entry.WeightKg : null,
                date == Today,
                date < Today && entriesByDate.ContainsKey(date)))
            .ToList();

        Summary = metricsService.BuildSummary(entries, Today, settings.WeekStartsOn, settings.GoalWeightKg);
        Chart = metricsService.BuildChartSeries(entries, settings.WeekStartsOn, settings.GoalWeightKg);
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

        var display = WeightConversionService.FromKilograms(valueKg.Value, DisplayUnit);
        return $"{display:+0.0;-0.0;0.0} {DisplayUnit}";
    }

    public string InputValue(DashboardDateCard card)
    {
        return card.WeightKg is null
            ? string.Empty
            : decimal.Round(WeightConversionService.FromKilograms(card.WeightKg.Value, DisplayUnit), 1).ToString("0.0");
    }
}
