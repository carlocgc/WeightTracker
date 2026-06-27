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
    public async Task Dashboard_LoadsTodayFirstDateCardFeed()
    {
        await using var app = new DashboardTestApp();
        await app.UpdateSettingsAsync("kg");
        await app.AddEntryAsync(Yesterday, 82.4m);
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Today's weight", html);
        Assert.Contains("Friday, 26 Jun 2026", html);
        Assert.Contains("Thursday, 25 Jun 2026", html);
        Assert.Contains("Current week", html);
        Assert.Contains("Previous week", html);
        Assert.Contains("inputmode=\"decimal\"", html);
        Assert.Contains("data-decimal-input", html);
        Assert.True(html.IndexOf("Friday, 26 Jun 2026", StringComparison.Ordinal) <
            html.IndexOf("Thursday, 25 Jun 2026", StringComparison.Ordinal));
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

        public async Task UpdateSettingsAsync(string displayUnit)
        {
            using var scope = Services.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
            await settings.UpdateAsync(displayUnit, null, DayOfWeek.Monday, "Europe/London", "dark");
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
