using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WeightTracker.Web.Data;
using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class DashboardPageTests
{
    private static readonly DateOnly Today = new(2026, 6, 26);
    private static readonly DateOnly Yesterday = new(2026, 6, 25);

    [Fact]
    public async Task Dashboard_RendersMobileDashboardWithCalendarEntryDialog()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(Today, 82.1m);
        await app.AddEntryAsync(Yesterday, 82.4m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("class=\"weight-app\"", html);
        Assert.Contains("class=\"dashboard-summary\"", html);
        Assert.Contains("class=\"dashboard-primary\"", html);
        Assert.Contains("class=\"dashboard-supporting\"", html);
        Assert.Single(Regex.Matches(html, "class=\"dashboard-summary\""));
        Assert.Single(Regex.Matches(html, "class=\"dashboard-primary\""));
        Assert.Single(Regex.Matches(html, "class=\"dashboard-supporting\""));
        Assert.Contains("Latest weight", html);
        Assert.Contains("82.1 kg", html);
        Assert.Contains("Recent history", html);
        Assert.Contains("class=\"trend-chart-frame\"", html);
        Assert.DoesNotContain("id=\"trendChart\" height=", html);
        Assert.Contains("Thursday, 25 Jun", html);
        Assert.Contains("Add / Update", html);
        Assert.Contains("role=\"dialog\"", html);
        Assert.Contains("data-entry-calendar", html);
        Assert.Contains("data-calendar-day=\"2026-06-25\"", html);
        Assert.Contains("data-entry-date=\"2026-06-25\"", html);
        Assert.Contains("data-entry-weight=\"82.40\"", html);
        Assert.DoesNotContain("entry-card", html);
        Assert.Contains("inputmode=\"decimal\"", html);
        Assert.Contains("data-decimal-input", html);
        Assert.DoesNotContain("class=\"validation-summary\"", html);
    }

    [Fact]
    public async Task Dashboard_CalendarNavigationKeepsEntryDialogOpenWhenChangingMonths()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        var client = app.CreateClient();

        var dashboardResponse = await client.GetAsync("/");
        var dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();

        Assert.True(dashboardResponse.StatusCode == HttpStatusCode.OK, dashboardHtml);
        Assert.Contains("href=\"?month=2026-05&amp;entry=true\"", dashboardHtml);

        var monthResponse = await client.GetAsync("/?month=2026-05&entry=true");
        var monthHtml = await monthResponse.Content.ReadAsStringAsync();

        Assert.True(monthResponse.StatusCode == HttpStatusCode.OK, monthHtml);
        Assert.Contains("id=\"entryDialog\"", monthHtml);
        Assert.Contains("data-open-entry-on-load=\"true\"", monthHtml);
    }

    [Fact]
    public async Task Dashboard_RendersEntryWeightsWithTwoDecimalPlacesInDialog()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(Today, 82.12m);
        await app.AddEntryAsync(Yesterday, 82.45m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("value=\"82.12\"", html);
        Assert.Contains("data-entry-weight=\"82.45\"", html);
    }

    [Fact]
    public async Task Dashboard_CalendarMonthQueryRendersHistoricalEntry()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(new DateOnly(2026, 5, 15), 83.2m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/?month=2026-05");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("May 2026", html);
        Assert.Contains("data-calendar-day=\"2026-05-15\"", html);
        Assert.Contains("data-entry-date=\"2026-05-15\"", html);
        Assert.Contains("data-entry-weight=\"83.20\"", html);
    }

    [Fact]
    public async Task Dashboard_RendersDeepInsightSectionsWithAllTimeData()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(new DateOnly(2025, 11, 1), 84.0m);
        await app.AddEntryAsync(new DateOnly(2026, 5, 20), 83.0m);
        await app.AddEntryAsync(Yesterday, 82.4m);
        await app.AddEntryAsync(Today, 82.1m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.DoesNotContain("Long-term trend", html);
        Assert.Contains("Progress insights", html);
        Assert.Contains("Goal forecast", html);
        Assert.Contains("Set a goal to unlock forecast", html);
        Assert.Contains("Set a goal to unlock goal-direction records.", html);
        Assert.DoesNotContain("id=\"longRangeTrendChart\"", html);
        Assert.Contains("data-trend-range=\"1m\"", html);
        Assert.Contains("data-trend-range=\"3m\"", html);
        Assert.Contains("data-trend-range=\"6m\"", html);
        Assert.Contains("data-trend-range=\"1y\"", html);
        Assert.Contains("data-trend-range=\"all\"", html);
        Assert.Contains("data-trend-range-label>Last month</span>", html);
        Assert.Contains("aria-pressed=\"true\">1M</button>", html);
        Assert.Contains("\"date\":\"2025-11-01\"", html);
        Assert.Contains("Latest", html);
        Assert.Contains("82.1 kg", html);
        Assert.Contains("High", html);
        Assert.Contains("84.0 kg", html);
        Assert.Contains("Low", html);
        Assert.Contains("-0.9 kg", html);
        Assert.Contains("-1.9 kg", html);
        Assert.Contains("Entry count", html);
        Assert.Contains(">4</strong>", html);
        Assert.DoesNotContain("full-history-list", html);
    }

    [Fact]
    public async Task Dashboard_WithNoEntries_RendersEmptyDeepInsights()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.DoesNotContain("Long-term trend", html);
        Assert.Contains("Progress insights", html);
        Assert.Contains("Goal forecast", html);
        Assert.Contains("Set a goal to unlock forecast", html);
        Assert.DoesNotContain("id=\"longRangeTrendChart\"", html);
        Assert.Contains("Entry count", html);
        Assert.Contains(">0</strong>", html);
        Assert.Contains("No weights recorded yet.", html);
    }

    [Fact]
    public async Task Dashboard_WithGoal_RendersForecastAndGoalDirectionRecords()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg", goalWeightKg: 80m);
        await app.AddEntryAsync(new DateOnly(2026, 5, 27), 86.0m);
        await app.AddEntryAsync(new DateOnly(2026, 6, 19), 84.0m);
        await app.AddEntryAsync(Today, 83.0m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        var decoded = WebUtility.HtmlDecode(html);

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("Progress insights", html);
        Assert.Contains("Goal forecast", html);
        Assert.Contains("Estimated Jul 2026", html);
        Assert.Contains("Based on 30-day pace", html);
        Assert.Contains("Best 7-day progress", html);
        Assert.Contains("Best 30-day progress", html);
        Assert.Contains("-1.0 kg", decoded);
        Assert.Contains("-3.0 kg", decoded);
        Assert.Contains("19 Jun to 26 Jun", html);
        Assert.Contains("27 May to 26 Jun", html);
        Assert.Contains("metric-status--toward", html);
        Assert.Contains("aria-label=\"30-day change:", html);
        Assert.Contains("toward goal", html);
        Assert.Contains("role=\"list\" aria-label=\"Personal records\"", html);
        Assert.Contains("role=\"listitem\"", html);
    }

    [Fact]
    public async Task Dashboard_WithGoalAndNoQualifyingRecords_RendersRecordEmptyState()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg", goalWeightKg: 80m);
        await app.AddEntryAsync(Today, 84.0m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("No goal-direction record yet.", html);
        Assert.DoesNotContain("Set a goal to unlock goal-direction records.", html);
    }

    [Fact]
    public async Task Dashboard_WithNoGoal_RendersGoalPanelAndSetAction()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("aria-label=\"Goal\"", html);
        Assert.Contains("No goal set", html);
        Assert.Contains("Set a target weight", html);
        Assert.Contains("aria-label=\"Set goal\"", html);
        Assert.Contains("id=\"goalDialog\"", html);
        Assert.Contains("name=\"GoalWeight\"", html);
        Assert.DoesNotContain("Clear goal", html);
    }

    [Fact]
    public async Task Dashboard_WithGoal_RendersGoalPanelAndEditAction()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg", goalWeightKg: 78m);
        await app.AddEntryAsync(Today, 82.1m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("aria-label=\"Goal\"", html);
        Assert.Contains("78.0 kg", html);
        Assert.Contains("+4.1 kg", WebUtility.HtmlDecode(html));
        Assert.Contains("aria-label=\"Edit goal\"", html);
        Assert.Contains("value=\"78.0\"", html);
        Assert.Contains("Clear goal", html);
    }

    [Fact]
    public async Task SaveGoal_WithSelectedDisplayUnit_PersistsConvertedGoal()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("lb", weekStartsOn: DayOfWeek.Sunday, timeZoneId: "Europe/London", theme: "light");
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetRequestVerificationTokenAsync(client);

        var response = await client.PostAsync("/?handler=Goal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["GoalWeight"] = "180"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var settings = await app.GetSettingsAsync();
        Assert.Equal("lb", settings.DisplayUnit);
        Assert.Equal(81.647m, settings.GoalWeightKg);
        Assert.Equal(DayOfWeek.Sunday, settings.WeekStartsOn);
        Assert.Equal("Europe/London", settings.TimeZoneId);
        Assert.Equal("light", settings.Theme);
    }

    [Fact]
    public async Task SaveGoal_WithInvalidGoal_ReturnsValidationAndReopensGoalDialog()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        var client = app.CreateClient();
        var token = await GetRequestVerificationTokenAsync(client);

        var response = await client.PostAsync("/?handler=Goal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["GoalWeight"] = "0"
        }));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Enter a goal greater than zero.", html);
        Assert.Contains("data-open-goal-on-load=\"true\"", html);
        Assert.Null((await app.GetSettingsAsync()).GoalWeightKg);
    }

    [Fact]
    public async Task ClearGoal_RemovesGoalAndPreservesOtherSettings()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("lb", goalWeightKg: 80m, weekStartsOn: DayOfWeek.Sunday, timeZoneId: "Europe/London", theme: "light");
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetRequestVerificationTokenAsync(client);

        var response = await client.PostAsync("/?handler=ClearGoal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var settings = await app.GetSettingsAsync();
        Assert.Equal("lb", settings.DisplayUnit);
        Assert.Null(settings.GoalWeightKg);
        Assert.Equal(DayOfWeek.Sunday, settings.WeekStartsOn);
        Assert.Equal("Europe/London", settings.TimeZoneId);
        Assert.Equal("light", settings.Theme);
    }

    [Fact]
    public async Task Save_WithInvalidWeight_ReturnsValidationAndDoesNotPersist()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        var client = app.CreateClient();
        var token = await GetRequestVerificationTokenAsync(client);

        var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["EntryDate"] = Today.ToString("O"),
            ["Weight"] = "0"
        }));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Enter a weight greater than zero.", html);
        Assert.Contains("validation-summary", html);
        Assert.Empty(await app.GetEntriesAsync());
    }

    [Fact]
    public async Task Save_WithPastCardDate_PersistsThatDate()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("lb");
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetRequestVerificationTokenAsync(client);

        var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["EntryDate"] = Yesterday.ToString("O"),
            ["Weight"] = "200"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var entry = Assert.Single(await app.GetEntriesAsync());
        Assert.Equal(Yesterday, entry.EntryDate);
        Assert.Equal(90.718m, entry.WeightKg);
    }

    [Fact]
    public async Task Delete_DeletesPastEntryAndRejectsToday()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(Yesterday, 82.4m);
        await app.AddEntryAsync(Today, 82.1m);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetRequestVerificationTokenAsync(client);

        var deletePastResponse = await client.PostAsync("/?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["EntryDate"] = Yesterday.ToString("O")
        }));

        Assert.Equal(HttpStatusCode.Redirect, deletePastResponse.StatusCode);
        Assert.DoesNotContain(await app.GetEntriesAsync(), entry => entry.EntryDate == Yesterday);

        token = await GetRequestVerificationTokenAsync(client);
        var deleteTodayResponse = await client.PostAsync("/?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["EntryDate"] = Today.ToString("O")
        }));
        var html = await deleteTodayResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, deleteTodayResponse.StatusCode);
        Assert.Contains("Only past entries can be deleted.", html);
        Assert.Contains(await app.GetEntriesAsync(), entry => entry.EntryDate == Today);
    }

    [Fact]
    public async Task Dashboard_RendersDataManagementSection()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("aria-label=\"Data management\"", html);
        Assert.Contains("href=\"/?handler=ExportCsv\"", html);
        Assert.Contains("data-open-import", html);
        Assert.Contains("id=\"importCsvDialog\"", html);
        Assert.Contains("name=\"ImportFile\"", html);
        Assert.Contains("Delete all", html);
        Assert.Contains("id=\"deleteAllWarningDialog\"", html);
        Assert.Contains("id=\"deleteAllConfirmDialog\"", html);
        Assert.DoesNotContain("class=\"data-upload\"", html);
        Assert.DoesNotContain("for=\"importFile\">Import CSV</label>", html);
    }

    [Fact]
    public async Task ExportCsv_ReturnsWeightCsvDownload()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(Yesterday, 82.4m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/?handler=ExportCsv");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("weighttracker-weights-20260626.csv", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("entry_date,weight_kg,note\n2026-06-25,82.400,\n", csv);
    }

    [Fact]
    public async Task ImportCsv_WithValidCsv_RedirectsAndPersistsEntries()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(Yesterday, 90m);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetRequestVerificationTokenAsync(client);
        using var form = CreateCsvUpload(token, "entry_date,weight_kg,note\n2026-06-25,82.400,updated\n2026-06-26,82.100,\n");

        var response = await client.PostAsync("/?handler=ImportCsv", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var entries = await app.GetEntriesAsync();
        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal(Yesterday, entry.EntryDate);
                Assert.Equal(82.400m, entry.WeightKg);
                Assert.Equal("updated", entry.Note);
            },
            entry =>
            {
                Assert.Equal(Today, entry.EntryDate);
                Assert.Equal(82.100m, entry.WeightKg);
                Assert.Null(entry.Note);
            });
    }

    [Fact]
    public async Task ImportCsv_WithInvalidCsv_ReturnsValidationAndDoesNotPartiallyPersist()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(Yesterday, 90m);
        var client = app.CreateClient();
        var token = await GetRequestVerificationTokenAsync(client);
        using var form = CreateCsvUpload(token, "entry_date,weight_kg,note\n2026-06-25,82.400,updated\n2026-06-26,1000.001,invalid\n");

        var response = await client.PostAsync("/?handler=ImportCsv", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Row 3", html);
        var entry = Assert.Single(await app.GetEntriesAsync());
        Assert.Equal(Yesterday, entry.EntryDate);
        Assert.Equal(90m, entry.WeightKg);
    }

    [Fact]
    public async Task DeleteAllWeights_WithInvalidConfirmation_ReturnsValidationAndPreservesEntries()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(Yesterday, 82.4m);
        var client = app.CreateClient();
        var token = await GetRequestVerificationTokenAsync(client);

        var response = await client.PostAsync("/?handler=DeleteAllWeights", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DeleteAllConfirmation"] = "delete"
        }));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Type DELETE", html);
        Assert.Single(await app.GetEntriesAsync());
    }

    [Fact]
    public async Task DeleteAllWeights_WithExactConfirmation_DeletesEntriesAndPreservesSettings()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("lb", goalWeightKg: 75m, weekStartsOn: DayOfWeek.Sunday, timeZoneId: "Europe/London", theme: "light");
        await app.AddEntryAsync(Yesterday, 82.4m);
        await app.AddEntryAsync(Today, 82.1m);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetRequestVerificationTokenAsync(client);

        var response = await client.PostAsync("/?handler=DeleteAllWeights", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DeleteAllConfirmation"] = "DELETE"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(await app.GetEntriesAsync());
        var settings = await app.GetSettingsAsync();
        Assert.Equal("lb", settings.DisplayUnit);
        Assert.Equal(75m, settings.GoalWeightKg);
        Assert.Equal(DayOfWeek.Sunday, settings.WeekStartsOn);
        Assert.Equal("Europe/London", settings.TimeZoneId);
        Assert.Equal("light", settings.Theme);
    }

    [Fact]
    public async Task ImportCsv_WithLargeCsv_RedirectsToDashboard()
    {
        await using var app = new DashboardTestApp(new DateTime(2026, 6, 28, 9, 0, 0, DateTimeKind.Utc));
        await app.UpdateSettingsAsync("kg");
        var client = app.CreateClient();
        var token = await GetRequestVerificationTokenAsync(client);
        using var form = CreateCsvUpload(token, BuildLargeCsv());

        var response = await client.PostAsync("/?handler=ImportCsv", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aria-label=\"Data management\"", html);
        Assert.Contains("Entry count", html);
        Assert.Contains(">791</strong>", html);
    }

    private static async Task<string> GetRequestVerificationTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/");
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");

        Assert.True(match.Success, "The dashboard did not render an antiforgery token.");
        return match.Groups["token"].Value;
    }

    private static MultipartFormDataContent CreateCsvUpload(string token, string csv)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" }
        };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "ImportFile", "weights.csv");
        return form;
    }

    private static string BuildLargeCsv()
    {
        var builder = new StringBuilder("entry_date,weight_kg,note\n");
        var start = new DateOnly(2024, 4, 29);
        for (var offset = 0; offset < 791; offset++)
        {
            builder.Append(start.AddDays(offset).ToString("yyyy-MM-dd"));
            builder.Append(",93.850,\n");
        }

        return builder.ToString();
    }

    private sealed class DashboardTestApp : WebApplicationFactory<Program>
    {
        private static readonly DateTime DefaultUtcNow = new(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc);

        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly DateTime _utcNow;
        private readonly string _dataProtectionKeysPath = Path.Combine(
            Path.GetTempPath(),
            "weighttracker-tests",
            Guid.NewGuid().ToString("N"),
            "DataProtectionKeys");

        public DashboardTestApp(DateTime? utcNow = null)
        {
            _utcNow = utcNow ?? DefaultUtcNow;
            _connection.Open();
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WeightTrackerDbContext>();
            db.Database.EnsureCreated();
        }

        public async Task UpdateSettingsAsync(
            string displayUnit,
            decimal? goalWeightKg = null,
            DayOfWeek weekStartsOn = DayOfWeek.Monday,
            string timeZoneId = "Europe/London",
            string theme = "dark")
        {
            using var scope = Services.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
            await settings.UpdateAsync(displayUnit, goalWeightKg, weekStartsOn, timeZoneId, theme);
        }

        public async Task AddEntryAsync(DateOnly entryDate, decimal weightKg)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WeightTrackerDbContext>();
            db.WeightEntries.Add(new WeightEntry
            {
                EntryDate = entryDate,
                WeightKg = weightKg,
                CreatedAtUtc = _utcNow,
                UpdatedAtUtc = _utcNow
            });
            await db.SaveChangesAsync();
        }

        public async Task<List<WeightEntry>> GetEntriesAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WeightTrackerDbContext>();
            return await db.WeightEntries
                .AsNoTracking()
                .OrderBy(entry => entry.EntryDate)
                .ToListAsync();
        }

        public async Task<AppSettings> GetSettingsAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WeightTrackerDbContext>();
            return await db.AppSettings.AsNoTracking().SingleAsync(settings => settings.Id == AppSettings.SingletonId);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("DataProtection:KeysPath", _dataProtectionKeysPath);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<WeightTrackerDbContext>>();
                services.RemoveAll<IClock>();
                services.AddDbContext<WeightTrackerDbContext>(options => options.UseSqlite(_connection));
                services.AddSingleton<IClock>(new FixedClock(_utcNow));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }
}
