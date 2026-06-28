using System.Net;
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
        Assert.Contains("data-entry-weight=\"82.4\"", html);
        Assert.DoesNotContain("entry-card", html);
        Assert.Contains("inputmode=\"decimal\"", html);
        Assert.Contains("data-decimal-input", html);
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
        Assert.Contains("data-entry-weight=\"83.2\"", html);
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
        Assert.Contains("Long-term trend", html);
        Assert.Contains("Insights", html);
        Assert.Contains("id=\"longRangeTrendChart\"", html);
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
        Assert.DoesNotContain("All entries", html);
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
        Assert.Contains("Long-term trend", html);
        Assert.Contains("Insights", html);
        Assert.Contains("id=\"longRangeTrendChart\"", html);
        Assert.Contains("Entry count", html);
        Assert.Contains(">0</strong>", html);
        Assert.Contains("No weights recorded yet.", html);
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

    private static async Task<string> GetRequestVerificationTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/");
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");

        Assert.True(match.Success, "The dashboard did not render an antiforgery token.");
        return match.Groups["token"].Value;
    }

    private sealed class DashboardTestApp : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public DashboardTestApp()
        {
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
                CreatedAtUtc = new DateTime(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc)
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
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<WeightTrackerDbContext>>();
                services.RemoveAll<IClock>();
                services.AddDbContext<WeightTrackerDbContext>(options => options.UseSqlite(_connection));
                services.AddSingleton<IClock>(new FixedClock(new DateTime(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc)));
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
