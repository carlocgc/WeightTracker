# Weight Tracker Initial App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first Dockerized ASP.NET Core WeightTracker app with daily weight entry, persisted settings, trend metrics, history graphs, dark mode, and Docker Compose deployment.

**Architecture:** Use an ASP.NET Core Razor Pages web app backed by SQLite through Entity Framework Core. Keep behavior in small application services so metrics, settings, and daily-entry upserts are testable without rendering pages and can later accept an authenticated user scope.

**Tech Stack:** .NET 10, ASP.NET Core Razor Pages, EF Core SQLite, xUnit, Chart.js, Docker Compose, custom CSS.

---

## Product Amendments (2026-06-26)

These approved requirements supersede the conflicting portions of Tasks 5, 7, and 9:

- The main entry page is a descending, scrollable date-card feed. Today's card is first on launch; earlier cards are editable without any user-entered date.
- A date card's local calendar date is derived from the saved application time zone. The persistence service receives that application-generated date internally so it can save the selected card, but the entry form accepts only weight in the configured unit.
- The weight field uses `inputmode="decimal"` to request a mobile numeric keypad. It filters input to digits and one decimal separator, while server-side decimal parsing and positive-value validation remain authoritative.
- A past card has a confirmed, antiforgery-protected deletion action. The service rejects deletion of today and future dates.

---


## File Structure

Create this structure:


```text
WeightTracker.sln
Directory.Build.props
src/WeightTracker.Web/WeightTracker.Web.csproj
src/WeightTracker.Web/Program.cs
src/WeightTracker.Web/appsettings.json
src/WeightTracker.Web/Data/WeightTrackerDbContext.cs
src/WeightTracker.Web/Models/AppSettings.cs
src/WeightTracker.Web/Models/WeightEntry.cs
src/WeightTracker.Web/Services/Clock.cs
src/WeightTracker.Web/Services/MetricsService.cs
src/WeightTracker.Web/Services/SettingsService.cs
src/WeightTracker.Web/Services/WeightConversionService.cs
src/WeightTracker.Web/Services/WeightEntryService.cs
src/WeightTracker.Web/Pages/Index.cshtml
src/WeightTracker.Web/Pages/Index.cshtml.cs
src/WeightTracker.Web/Pages/History.cshtml
src/WeightTracker.Web/Pages/History.cshtml.cs
src/WeightTracker.Web/Pages/Settings.cshtml
src/WeightTracker.Web/Pages/Settings.cshtml.cs
src/WeightTracker.Web/Pages/Shared/_Layout.cshtml
src/WeightTracker.Web/Pages/Shared/_ValidationScriptsPartial.cshtml
src/WeightTracker.Web/wwwroot/css/site.css
src/WeightTracker.Web/Dockerfile
tests/WeightTracker.Tests/WeightTracker.Tests.csproj
tests/WeightTracker.Tests/ServiceTestFixture.cs
tests/WeightTracker.Tests/WeightConversionServiceTests.cs
tests/WeightTracker.Tests/WeightEntryServiceTests.cs
tests/WeightTracker.Tests/MetricsServiceTests.cs
tests/WeightTracker.Tests/SettingsServiceTests.cs
tests/WeightTracker.Tests/DashboardPageTests.cs
docker-compose.yml
README.md
```

Responsibilities:

- `Models`: persistence entities only.
- `Data`: EF Core context and model constraints.
- `Services`: business rules, metrics, unit conversion, settings persistence, date/time abstraction.
- `Pages`: Razor page rendering and form handling.
- `wwwroot/css/site.css`: all app styling, including dark mode and responsive layout.
- `tests`: unit and integration tests.

## Task 1: Scaffold The .NET Solution

**Files:**
- Create: `WeightTracker.sln`
- Create: `Directory.Build.props`
- Create: `src/WeightTracker.Web/WeightTracker.Web.csproj`
- Create: `tests/WeightTracker.Tests/WeightTracker.Tests.csproj`
- Modify: `README.md`

- [ ] **Step 1: Create the solution and projects**

Run:

```powershell
dotnet new sln -n WeightTracker
dotnet new webapp -n WeightTracker.Web -o src/WeightTracker.Web --framework net10.0
dotnet new xunit -n WeightTracker.Tests -o tests/WeightTracker.Tests --framework net10.0
dotnet sln WeightTracker.sln add src/WeightTracker.Web/WeightTracker.Web.csproj
dotnet sln WeightTracker.sln add tests/WeightTracker.Tests/WeightTracker.Tests.csproj
dotnet add tests/WeightTracker.Tests/WeightTracker.Tests.csproj reference src/WeightTracker.Web/WeightTracker.Web.csproj
```

Expected: solution and two projects are created.

- [ ] **Step 2: Add required NuGet packages**

Run:

```powershell
dotnet add src/WeightTracker.Web/WeightTracker.Web.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/WeightTracker.Web/WeightTracker.Web.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add tests/WeightTracker.Tests/WeightTracker.Tests.csproj package Microsoft.Data.Sqlite
dotnet add tests/WeightTracker.Tests/WeightTracker.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/WeightTracker.Tests/WeightTracker.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

Expected: restore succeeds. If restore fails because network is blocked, rerun with approved network access.

- [ ] **Step 3: Add shared build settings**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Build the empty scaffold**

Run:

```powershell
dotnet build WeightTracker.sln
```

Expected: build passes.

- [ ] **Step 5: Commit**

Run:

```powershell
git add WeightTracker.sln Directory.Build.props src tests README.md
git commit -m "chore: scaffold dotnet solution"
```

## Task 2: Add Persistence Models And DbContext

**Files:**
- Create: `src/WeightTracker.Web/Models/WeightEntry.cs`
- Create: `src/WeightTracker.Web/Models/AppSettings.cs`
- Create: `src/WeightTracker.Web/Data/WeightTrackerDbContext.cs`
- Modify: `src/WeightTracker.Web/Program.cs`
- Modify: `src/WeightTracker.Web/appsettings.json`
- Test: `tests/WeightTracker.Tests/ServiceTestFixture.cs`

- [ ] **Step 1: Write the database fixture used by service tests**

Create `tests/WeightTracker.Tests/ServiceTestFixture.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;

namespace WeightTracker.Tests;

public sealed class ServiceTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public ServiceTestFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public WeightTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WeightTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        var db = new WeightTrackerDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
```

Expected before implementation: compile fails because `WeightTrackerDbContext` does not exist.

- [ ] **Step 2: Add entities**

Create `src/WeightTracker.Web/Models/WeightEntry.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace WeightTracker.Web.Models;

public sealed class WeightEntry
{
    public int Id { get; set; }

    public DateOnly EntryDate { get; set; }

    [Range(0.1, 1000)]
    public decimal WeightKg { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
```

Create `src/WeightTracker.Web/Models/AppSettings.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace WeightTracker.Web.Models;

public sealed class AppSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    [StringLength(2)]
    public string DisplayUnit { get; set; } = "kg";

    public decimal? GoalWeightKg { get; set; }

    public DayOfWeek WeekStartsOn { get; set; } = DayOfWeek.Monday;

    [StringLength(100)]
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

    [StringLength(10)]
    public string Theme { get; set; } = "dark";
}
```

- [ ] **Step 3: Add EF Core context**

Create `src/WeightTracker.Web/Data/WeightTrackerDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Data;

public sealed class WeightTrackerDbContext(DbContextOptions<WeightTrackerDbContext> options)
    : DbContext(options)
{
    public DbSet<WeightEntry> WeightEntries => Set<WeightEntry>();

    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeightEntry>(entity =>
        {
            entity.HasIndex(entry => entry.EntryDate).IsUnique();
            entity.Property(entry => entry.WeightKg).HasColumnType("decimal(8,3)");
            entity.Property(entry => entry.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.Property(settings => settings.DisplayUnit).HasMaxLength(2);
            entity.Property(settings => settings.GoalWeightKg).HasColumnType("decimal(8,3)");
            entity.Property(settings => settings.TimeZoneId).HasMaxLength(100);
            entity.Property(settings => settings.Theme).HasMaxLength(10);
        });
    }
}
```

- [ ] **Step 4: Configure SQLite**

Update `src/WeightTracker.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "WeightTracker": "Data Source=App_Data/weighttracker.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Update `src/WeightTracker.Web/Program.cs` so it contains:

```csharp
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<WeightTrackerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WeightTracker")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.Run();

public partial class Program;
```

- [ ] **Step 5: Run build**

Run:

```powershell
dotnet build WeightTracker.sln
```

Expected: build passes.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/WeightTracker.Web tests/WeightTracker.Tests
git commit -m "feat: add persistence model"
```

## Task 3: Add Weight Conversion

**Files:**
- Create: `src/WeightTracker.Web/Services/WeightConversionService.cs`
- Test: `tests/WeightTracker.Tests/WeightConversionServiceTests.cs`

- [ ] **Step 1: Write failing conversion tests**

Create `tests/WeightTracker.Tests/WeightConversionServiceTests.cs`:

```csharp
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class WeightConversionServiceTests
{
    [Fact]
    public void ToKilograms_ReturnsInput_WhenUnitIsKilograms()
    {
        Assert.Equal(82.4m, WeightConversionService.ToKilograms(82.4m, "kg"));
    }

    [Fact]
    public void ToKilograms_ConvertsPoundsToKilograms()
    {
        Assert.Equal(90.718m, Math.Round(WeightConversionService.ToKilograms(200m, "lb"), 3));
    }

    [Fact]
    public void FromKilograms_ConvertsKilogramsToPounds()
    {
        Assert.Equal(200.000m, Math.Round(WeightConversionService.FromKilograms(90.718474m, "lb"), 3));
    }

    [Fact]
    public void ToKilograms_RejectsUnsupportedUnit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeightConversionService.ToKilograms(80m, "stone"));
    }
}
```

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter WeightConversionServiceTests
```

Expected: fails because `WeightConversionService` does not exist.

- [ ] **Step 2: Implement conversion service**

Create `src/WeightTracker.Web/Services/WeightConversionService.cs`:

```csharp
namespace WeightTracker.Web.Services;

public static class WeightConversionService
{
    private const decimal PoundsPerKilogram = 2.20462262185m;

    public static decimal ToKilograms(decimal value, string unit)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Weight must be greater than zero.");
        }

        return NormalizeUnit(unit) switch
        {
            "kg" => value,
            "lb" => value / PoundsPerKilogram,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), "Supported units are kg and lb.")
        };
    }

    public static decimal FromKilograms(decimal valueKg, string unit)
    {
        if (valueKg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueKg), "Weight must be greater than zero.");
        }

        return NormalizeUnit(unit) switch
        {
            "kg" => valueKg,
            "lb" => valueKg * PoundsPerKilogram,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), "Supported units are kg and lb.")
        };
    }

    public static string NormalizeUnit(string unit)
    {
        return unit.Trim().ToLowerInvariant();
    }
}
```

- [ ] **Step 3: Run conversion tests**

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter WeightConversionServiceTests
```

Expected: tests pass.

- [ ] **Step 4: Commit**

Run:

```powershell
git add src/WeightTracker.Web/Services/WeightConversionService.cs tests/WeightTracker.Tests/WeightConversionServiceTests.cs
git commit -m "feat: add weight conversion service"
```

## Task 4: Add Settings Service

**Files:**
- Create: `src/WeightTracker.Web/Services/SettingsService.cs`
- Test: `tests/WeightTracker.Tests/SettingsServiceTests.cs`
- Modify: `src/WeightTracker.Web/Program.cs`

- [ ] **Step 1: Write failing settings tests**

Create `tests/WeightTracker.Tests/SettingsServiceTests.cs`:

```csharp
using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task GetAsync_CreatesDarkDefaultSettings()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = new SettingsService(db);

        var settings = await service.GetAsync();

        Assert.Equal(AppSettings.SingletonId, settings.Id);
        Assert.Equal("kg", settings.DisplayUnit);
        Assert.Equal("dark", settings.Theme);
        Assert.Equal(DayOfWeek.Monday, settings.WeekStartsOn);
    }

    [Fact]
    public async Task UpdateAsync_PersistsThemeAndGoal()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = new SettingsService(db);

        await service.UpdateAsync("lb", 190m, DayOfWeek.Sunday, "Europe/London", "light");
        var settings = await service.GetAsync();

        Assert.Equal("lb", settings.DisplayUnit);
        Assert.Equal(190m, settings.GoalWeightKg);
        Assert.Equal(DayOfWeek.Sunday, settings.WeekStartsOn);
        Assert.Equal("Europe/London", settings.TimeZoneId);
        Assert.Equal("light", settings.Theme);
    }
}
```

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter SettingsServiceTests
```

Expected: fails because `SettingsService` does not exist.

- [ ] **Step 2: Implement settings service**

Create `src/WeightTracker.Web/Services/SettingsService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Services;

public sealed class SettingsService(WeightTrackerDbContext db)
{
    private static readonly HashSet<string> ValidUnits = ["kg", "lb"];
    private static readonly HashSet<string> ValidThemes = ["dark", "light", "system"];

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.AppSettings.FindAsync([AppSettings.SingletonId], cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new AppSettings();
        db.AppSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<AppSettings> UpdateAsync(
        string displayUnit,
        decimal? goalWeightKg,
        DayOfWeek weekStartsOn,
        string timeZoneId,
        string theme,
        CancellationToken cancellationToken = default)
    {
        displayUnit = WeightConversionService.NormalizeUnit(displayUnit);
        theme = theme.Trim().ToLowerInvariant();

        if (!ValidUnits.Contains(displayUnit))
        {
            throw new ArgumentOutOfRangeException(nameof(displayUnit), "Supported units are kg and lb.");
        }

        if (!ValidThemes.Contains(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), "Supported themes are dark, light, and system.");
        }

        if (goalWeightKg is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(goalWeightKg), "Goal weight must be greater than zero.");
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        var settings = await db.AppSettings.SingleOrDefaultAsync(
            item => item.Id == AppSettings.SingletonId,
            cancellationToken);

        if (settings is null)
        {
            settings = new AppSettings();
            db.AppSettings.Add(settings);
        }

        settings.DisplayUnit = displayUnit;
        settings.GoalWeightKg = goalWeightKg;
        settings.WeekStartsOn = weekStartsOn;
        settings.TimeZoneId = timeZoneId;
        settings.Theme = theme;

        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
```

- [ ] **Step 3: Register settings service**

In `src/WeightTracker.Web/Program.cs`, add:

```csharp
using WeightTracker.Web.Services;
```

and register:

```csharp
builder.Services.AddScoped<SettingsService>();
```

Place the registration after `AddDbContext`.

- [ ] **Step 4: Run settings tests**

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter SettingsServiceTests
```

Expected: tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/WeightTracker.Web tests/WeightTracker.Tests/SettingsServiceTests.cs
git commit -m "feat: add settings service"
```

## Task 5: Add Daily Entry Upsert Service

**Files:**
- Create: `src/WeightTracker.Web/Services/Clock.cs`
- Create: `src/WeightTracker.Web/Services/WeightEntryService.cs`
- Test: `tests/WeightTracker.Tests/WeightEntryServiceTests.cs`
- Modify: `src/WeightTracker.Web/Program.cs`

- [ ] **Step 1: Write failing daily entry tests**

Create `tests/WeightTracker.Tests/WeightEntryServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class WeightEntryServiceTests
{
    [Fact]
    public async Task SaveAsync_InsertsEntry_WhenDateDoesNotExist()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = new WeightEntryService(db, new FixedClock(new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc)));

        await service.SaveAsync(new DateOnly(2026, 6, 25), 82.5m, "kg", "morning");

        var entry = await db.WeightEntries.SingleAsync();
        Assert.Equal(new DateOnly(2026, 6, 25), entry.EntryDate);
        Assert.Equal(82.5m, entry.WeightKg);
        Assert.Equal("morning", entry.Note);
    }

    [Fact]
    public async Task SaveAsync_UpdatesEntry_WhenDateAlreadyExists()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = new WeightEntryService(db, new FixedClock(new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc)));

        await service.SaveAsync(new DateOnly(2026, 6, 25), 82.5m, "kg", null);
        await service.SaveAsync(new DateOnly(2026, 6, 25), 82.1m, "kg", "corrected");

        var entries = await db.WeightEntries.ToListAsync();
        Assert.Single(entries);
        Assert.Equal(82.1m, entries[0].WeightKg);
        Assert.Equal("corrected", entries[0].Note);
    }

    [Fact]
    public async Task SaveAsync_StoresPoundInputAsKilograms()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = new WeightEntryService(db, new FixedClock(new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc)));

        await service.SaveAsync(new DateOnly(2026, 6, 25), 200m, "lb", null);

        var entry = await db.WeightEntries.SingleAsync();
        Assert.Equal(90.718m, Math.Round(entry.WeightKg, 3));
    }
}
```

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter WeightEntryServiceTests
```

Expected: fails because `WeightEntryService`, `FixedClock`, and clock abstractions do not exist.

- [ ] **Step 2: Add clock abstraction**

Create `src/WeightTracker.Web/Services/Clock.cs`:

```csharp
namespace WeightTracker.Web.Services;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}
```

- [ ] **Step 3: Implement entry service**

Create `src/WeightTracker.Web/Services/WeightEntryService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Services;

public sealed class WeightEntryService(WeightTrackerDbContext db, IClock clock)
{
    public async Task<WeightEntry> SaveAsync(
        DateOnly entryDate,
        decimal weight,
        string displayUnit,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var weightKg = decimal.Round(WeightConversionService.ToKilograms(weight, displayUnit), 3);
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        var entry = await db.WeightEntries.SingleOrDefaultAsync(
            item => item.EntryDate == entryDate,
            cancellationToken);

        if (entry is null)
        {
            entry = new WeightEntry
            {
                EntryDate = entryDate,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.WeightEntries.Add(entry);
        }

        entry.WeightKg = weightKg;
        entry.Note = cleanNote;
        entry.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public Task<WeightEntry?> GetByDateAsync(DateOnly entryDate, CancellationToken cancellationToken = default)
    {
        return db.WeightEntries.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EntryDate == entryDate, cancellationToken);
    }

    public Task<List<WeightEntry>> GetRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        return db.WeightEntries.AsNoTracking()
            .Where(item => item.EntryDate >= start && item.EntryDate <= end)
            .OrderBy(item => item.EntryDate)
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Register services**

In `Program.cs`, add:

```csharp
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<WeightEntryService>();
```

- [ ] **Step 5: Run daily entry tests**

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter WeightEntryServiceTests
```

Expected: tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/WeightTracker.Web tests/WeightTracker.Tests/WeightEntryServiceTests.cs
git commit -m "feat: add daily weight entry service"
```

## Task 6: Add Metrics Service

**Files:**
- Create: `src/WeightTracker.Web/Services/MetricsService.cs`
- Test: `tests/WeightTracker.Tests/MetricsServiceTests.cs`
- Modify: `src/WeightTracker.Web/Program.cs`

- [x] **Step 1: Write failing metrics tests**

Create `tests/WeightTracker.Tests/MetricsServiceTests.cs`:

```csharp
using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class MetricsServiceTests
{
    [Fact]
    public void BuildSummary_ComparesCurrentAndPreviousWeeklyAverages()
    {
        var entries = new[]
        {
            Entry("2026-06-15", 84.0m),
            Entry("2026-06-16", 83.0m),
            Entry("2026-06-22", 82.0m),
            Entry("2026-06-23", 81.0m)
        };

        var summary = service.BuildSummary(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, null);

        Assert.Equal(81.5m, summary.CurrentWeekAverageKg);
        Assert.Equal(83.5m, summary.PreviousWeekAverageKg);
        Assert.Equal(-2.0m, summary.WeekOverWeekDeltaKg);
    }

    [Fact]
    public void BuildSummary_UsesRecordedEntriesOnlyForMovingAverage()
    {
        var entries = new[]
        {
            Entry("2026-06-18", 82.0m),
            Entry("2026-06-22", 81.0m),
            Entry("2026-06-25", 80.0m)
        };

        var summary = service.BuildSummary(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, null);

        Assert.Equal(81.0m, summary.SevenDayMovingAverageKg);
    }

    [Fact]
    public void BuildChartSeries_ReturnsWeeklyAveragePoints()
    {
        var entries = new[]
        {
            Entry("2026-06-15", 84.0m),
            Entry("2026-06-16", 83.0m),
            Entry("2026-06-22", 82.0m),
            Entry("2026-06-23", 81.0m)
        };

        var series = service.BuildChartSeries(entries, DayOfWeek.Monday, null);

        Assert.Equal(4, series.DailyWeights.Count);
        Assert.Equal(2, series.WeeklyAverages.Count);
        Assert.Equal(83.5m, series.WeeklyAverages[0].WeightKg);
        Assert.Equal(81.5m, series.WeeklyAverages[1].WeightKg);
    }

    private static WeightEntry Entry(string date, decimal weightKg)
    {
        return new WeightEntry
        {
            EntryDate = DateOnly.Parse(date),
            WeightKg = weightKg,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
```

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter MetricsServiceTests
```

Expected: fails because `MetricsService` does not exist.

- [x] **Step 2: Implement metrics service**

Create `src/WeightTracker.Web/Services/MetricsService.cs`:

```csharp
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Services;

public sealed record MetricPoint(DateOnly Date, decimal WeightKg);

public sealed record MetricsSummary(
    decimal? LatestWeightKg,
    decimal? CurrentWeekAverageKg,
    decimal? PreviousWeekAverageKg,
    decimal? WeekOverWeekDeltaKg,
    decimal? SevenDayMovingAverageKg,
    decimal? ThirtyDayChangeKg,
    decimal? NinetyDayChangeKg,
    decimal? RangeHighKg,
    decimal? RangeLowKg,
    decimal? GoalWeightKg);

public sealed record ChartSeries(
    IReadOnlyList<MetricPoint> DailyWeights,
    IReadOnlyList<MetricPoint> WeeklyAverages,
    IReadOnlyList<MetricPoint> MovingAverages,
    decimal? GoalWeightKg);

public sealed class MetricsService
{
    public MetricsSummary BuildSummary(
        IEnumerable<WeightEntry> source,
        DateOnly today,
        DayOfWeek weekStartsOn,
        decimal? goalWeightKg)
    {
        var entries = source.OrderBy(item => item.EntryDate).ToList();
        if (entries.Count == 0)
        {
            return new MetricsSummary(null, null, null, null, null, null, null, null, null, goalWeightKg);
        }

        var currentWeekStart = StartOfWeek(today, weekStartsOn);
        var previousWeekStart = currentWeekStart.AddDays(-7);
        var previousWeekEnd = currentWeekStart.AddDays(-1);

        var currentWeekAverage = AverageForRange(entries, currentWeekStart, today);
        var previousWeekAverage = AverageForRange(entries, previousWeekStart, previousWeekEnd);
        var weekDelta = currentWeekAverage.HasValue && previousWeekAverage.HasValue
            ? currentWeekAverage.Value - previousWeekAverage.Value
            : null;

        return new MetricsSummary(
            LatestWeightKg: entries[^1].WeightKg,
            CurrentWeekAverageKg: currentWeekAverage,
            PreviousWeekAverageKg: previousWeekAverage,
            WeekOverWeekDeltaKg: weekDelta,
            SevenDayMovingAverageKg: AverageForRange(entries, today.AddDays(-6), today),
            ThirtyDayChangeKg: ChangeSince(entries, today.AddDays(-30)),
            NinetyDayChangeKg: ChangeSince(entries, today.AddDays(-90)),
            RangeHighKg: entries.Max(item => item.WeightKg),
            RangeLowKg: entries.Min(item => item.WeightKg),
            GoalWeightKg: goalWeightKg);
    }

    public ChartSeries BuildChartSeries(
        IEnumerable<WeightEntry> source,
        DayOfWeek weekStartsOn,
        decimal? goalWeightKg)
    {
        var entries = source.OrderBy(item => item.EntryDate).ToList();
        var daily = entries
            .Select(item => new MetricPoint(item.EntryDate, item.WeightKg))
            .ToList();

        var weekly = entries
            .GroupBy(item => StartOfWeek(item.EntryDate, weekStartsOn))
            .OrderBy(group => group.Key)
            .Select(group => new MetricPoint(group.Key, decimal.Round(group.Average(item => item.WeightKg), 3)))
            .ToList();

        var moving = entries
            .Select(item => new MetricPoint(
                item.EntryDate,
                AverageForRange(entries, item.EntryDate.AddDays(-6), item.EntryDate)!.Value))
            .ToList();

        return new ChartSeries(daily, weekly, moving, goalWeightKg);
    }


    private static DateOnly StartOfWeek(DateOnly date, DayOfWeek weekStartsOn)
    {
        var diff = (7 + (date.DayOfWeek - weekStartsOn)) % 7;
        return date.AddDays(-diff);
    }

    private static decimal? AverageForRange(IReadOnlyCollection<WeightEntry> entries, DateOnly start, DateOnly end)
    {
        var values = entries
            .Where(item => item.EntryDate >= start && item.EntryDate <= end)
            .Select(item => item.WeightKg)
            .ToList();

        return values.Count == 0 ? null : decimal.Round(values.Average(), 3);
    }

    private static decimal? ChangeSince(IReadOnlyList<WeightEntry> entries, DateOnly since)
    {
        var latest = entries[^1];
        var baseline = entries.LastOrDefault(item => item.EntryDate <= since) ??
                       entries.FirstOrDefault(item => item.EntryDate >= since);

        return baseline is null ? null : latest.WeightKg - baseline.WeightKg;
    }
}
```

- [x] **Step 3: Register metrics service**

In `Program.cs`, add:

```csharp
builder.Services.AddScoped<MetricsService>();
```

- [x] **Step 4: Run metrics tests**

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter MetricsServiceTests
```

Expected: tests pass.

- [x] **Step 5: Commit**

Run:

```powershell
git add src/WeightTracker.Web/Services/MetricsService.cs tests/WeightTracker.Tests/MetricsServiceTests.cs src/WeightTracker.Web/Program.cs
git commit -m "feat: add trend metrics service"
```

## Task 7: Build Dashboard Page

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml.cs`
- Test: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Write failing dashboard integration tests**

Create `tests/WeightTracker.Tests/DashboardPageTests.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.Tests;

public sealed class DashboardPageTests
{
    [Fact]
    public async Task Dashboard_LoadsEntryFirstContent()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Today's weight", html);
        Assert.Contains("Current week", html);
        Assert.Contains("Previous week", html);
    }
}
```

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter DashboardPageTests
```

Expected: fails until the dashboard content is implemented and database initialization is safe for tests.

- [ ] **Step 2: Implement dashboard page model**

Replace `src/WeightTracker.Web/Pages/Index.cshtml.cs` with:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeightTracker.Web.Services;

namespace WeightTracker.Web.Pages;

public sealed class IndexModel(
    SettingsService settingsService,
    WeightEntryService entryService,
    MetricsService metricsService) : PageModel
{
    [BindProperty]
    public decimal? Weight { get; set; }

    [BindProperty]
    public string? Note { get; set; }

    public string DisplayUnit { get; private set; } = "kg";
    public string Theme { get; private set; } = "dark";
    public DateOnly Today { get; private set; }
    public MetricsSummary Summary { get; private set; } = new(null, null, null, null, null, null, null, null, null, null);
    public ChartSeries Chart { get; private set; } = new([], [], [], null);
    public bool HasTodayEntry { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        Today = CurrentDate(settings.TimeZoneId);

        if (Weight is null or <= 0)
        {
            ModelState.AddModelError(nameof(Weight), "Enter a weight greater than zero.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        await entryService.SaveAsync(Today, Weight.Value, settings.DisplayUnit, Note, cancellationToken);
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        DisplayUnit = settings.DisplayUnit;
        Theme = settings.Theme;
        Today = CurrentDate(settings.TimeZoneId);

        var entries = await entryService.GetRangeAsync(Today.AddDays(-180), Today, cancellationToken);
        var todayEntry = entries.SingleOrDefault(item => item.EntryDate == Today);
        HasTodayEntry = todayEntry is not null;
        Weight = todayEntry is null ? null : decimal.Round(WeightConversionService.FromKilograms(todayEntry.WeightKg, DisplayUnit), 1);
        Note = todayEntry?.Note;
        Summary = metricsService.BuildSummary(entries, Today, settings.WeekStartsOn, settings.GoalWeightKg);
        Chart = metricsService.BuildChartSeries(entries, settings.WeekStartsOn, settings.GoalWeightKg);
    }

    private static DateOnly CurrentDate(string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    }
}
```

- [ ] **Step 3: Implement dashboard markup**

Replace `src/WeightTracker.Web/Pages/Index.cshtml` with:

```cshtml
@page
@model WeightTracker.Web.Pages.IndexModel
@{
    ViewData["Title"] = "Dashboard";
    ViewData["Theme"] = Model.Theme;
}

<section class="entry-panel">
    <div>
        <p class="eyebrow">@Model.Today.ToString("dddd, dd MMM yyyy")</p>
        <h1>Today's weight</h1>
        <p class="status">@(Model.HasTodayEntry ? "Today is logged. Saving again updates it." : "No entry saved for today yet.")</p>
    </div>

    <form method="post" class="entry-form">
        <label asp-for="Weight">Weight (@Model.DisplayUnit)</label>
        <input asp-for="Weight" type="number" inputmode="decimal" step="0.1" min="0.1" autofocus />
        <span asp-validation-for="Weight"></span>

        <label asp-for="Note">Note</label>
        <input asp-for="Note" maxlength="500" />

        <button type="submit">Save weight</button>
    </form>
</section>

<section class="metric-grid">
    <article><span>Latest</span><strong>@Format(Model.Summary.LatestWeightKg)</strong></article>
    <article><span>Current week</span><strong>@Format(Model.Summary.CurrentWeekAverageKg)</strong></article>
    <article><span>Previous week</span><strong>@Format(Model.Summary.PreviousWeekAverageKg)</strong></article>
    <article><span>Week delta</span><strong>@FormatSigned(Model.Summary.WeekOverWeekDeltaKg)</strong></article>
</section>

<section class="chart-panel">
    <div class="section-heading">
        <h2>Trend</h2>
        <a asp-page="/History">History</a>
    </div>
    <canvas id="trendChart" height="260"></canvas>
</section>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <script>
        const daily = @Json.Serialize(Model.Chart.DailyWeights);
        const weekly = @Json.Serialize(Model.Chart.WeeklyAverages);
        const moving = @Json.Serialize(Model.Chart.MovingAverages);
        const goal = @Json.Serialize(Model.Chart.GoalWeightKg);
        const labels = daily.map(point => point.date);
        new Chart(document.getElementById('trendChart'), {
            type: 'line',
            data: {
                labels,
                datasets: [
                    { label: 'Daily', data: daily.map(point => point.weightKg), borderColor: '#64d2ff', tension: 0.25 },
                    { label: '7-day avg', data: moving.map(point => point.weightKg), borderColor: '#ffd166', tension: 0.25 },
                    { label: 'Goal', data: goal ? labels.map(() => goal) : [], borderColor: '#7bd88f', borderDash: [6, 6], pointRadius: 0 }
                ]
            },
            options: { responsive: true, maintainAspectRatio: false }
        });
    </script>
}

@functions {
    private string Format(decimal? valueKg)
    {
        return valueKg is null ? "-" : $"{WeightTracker.Web.Services.WeightConversionService.FromKilograms(valueKg.Value, Model.DisplayUnit):0.0} {Model.DisplayUnit}";
    }

    private string FormatSigned(decimal? valueKg)
    {
        if (valueKg is null)
        {
            return "-";
        }

        var display = WeightTracker.Web.Services.WeightConversionService.FromKilograms(valueKg.Value, Model.DisplayUnit);
        return $"{display:+0.0;-0.0;0.0} {Model.DisplayUnit}";
    }
}
```

- [ ] **Step 4: Run dashboard test**

Run:

```powershell
dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --filter DashboardPageTests
```

Expected: test passes.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/WeightTracker.Web/Pages tests/WeightTracker.Tests/DashboardPageTests.cs
git commit -m "feat: add entry-first dashboard"
```

## Task 8: Build Layout And Dark Theme

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Shared/_Layout.cshtml`
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`

- [ ] **Step 1: Replace layout with app navigation**

Replace `src/WeightTracker.Web/Pages/Shared/_Layout.cshtml` with:

```cshtml
<!DOCTYPE html>
<html lang="en" data-theme="@(ViewData["Theme"] ?? "dark")">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - WeightTracker</title>
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body>
    <header class="app-header">
        <a class="brand" asp-page="/Index">WeightTracker</a>
        <nav>
            <a asp-page="/Index">Today</a>
            <a asp-page="/History">History</a>
            <a asp-page="/Settings">Settings</a>
        </nav>
    </header>

    <main class="app-shell">
        @RenderBody()
    </main>

    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

- [ ] **Step 2: Add responsive dark-first CSS**

Replace `src/WeightTracker.Web/wwwroot/css/site.css` with:

```css
:root {
  color-scheme: dark;
  --bg: #111418;
  --panel: #191f26;
  --panel-strong: #202832;
  --text: #f4f7fb;
  --muted: #a9b4c0;
  --line: #2d3743;
  --accent: #64d2ff;
  --accent-strong: #2fb8ed;
  --danger: #ff6b6b;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}

html[data-theme="light"] {
  color-scheme: light;
  --bg: #f6f8fb;
  --panel: #ffffff;
  --panel-strong: #edf2f7;
  --text: #17202a;
  --muted: #596675;
  --line: #d6dee8;
  --accent: #0969da;
  --accent-strong: #0757b8;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  min-height: 100vh;
  background: var(--bg);
  color: var(--text);
}

a {
  color: var(--accent);
  text-decoration: none;
}

.app-header {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.85rem 1rem;
  background: color-mix(in srgb, var(--bg) 92%, transparent);
  border-bottom: 1px solid var(--line);
  backdrop-filter: blur(12px);
}

.brand {
  color: var(--text);
  font-weight: 700;
}

nav {
  display: flex;
  gap: 0.8rem;
  font-size: 0.95rem;
}

.app-shell {
  width: min(1120px, 100%);
  margin: 0 auto;
  padding: 1rem;
}

.entry-panel,
.chart-panel,
.form-panel {
  display: grid;
  gap: 1rem;
  padding: 1rem;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 8px;
}

.entry-panel h1 {
  margin: 0.15rem 0;
  font-size: clamp(2rem, 10vw, 3.4rem);
  line-height: 1;
}

.eyebrow,
.status,
.section-heading span,
label,
.metric-grid span {
  color: var(--muted);
}

.entry-form,
.settings-form {
  display: grid;
  gap: 0.7rem;
}

input,
select,
button {
  width: 100%;
  border-radius: 6px;
  border: 1px solid var(--line);
  padding: 0.85rem;
  font: inherit;
}

input,
select {
  background: var(--panel-strong);
  color: var(--text);
}

button {
  border-color: var(--accent-strong);
  background: var(--accent);
  color: #051018;
  font-weight: 700;
  cursor: pointer;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
  margin: 1rem 0;
}

.metric-grid article {
  min-width: 0;
  padding: 0.9rem;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 8px;
}

.metric-grid strong {
  display: block;
  margin-top: 0.35rem;
  font-size: 1.25rem;
  overflow-wrap: anywhere;
}

.section-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.section-heading h2 {
  margin: 0;
}

.chart-panel {
  min-height: 360px;
}

.table-wrap {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
}

th,
td {
  padding: 0.75rem;
  border-bottom: 1px solid var(--line);
  text-align: left;
}

.field-validation-error {
  color: var(--danger);
}

@media (min-width: 760px) {
  .app-shell {
    padding: 1.5rem;
  }

  .entry-panel {
    grid-template-columns: 1.15fr 0.85fr;
    align-items: end;
  }

  .metric-grid {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}
```

- [ ] **Step 3: Run build**

Run:

```powershell
dotnet build WeightTracker.sln
```

Expected: build passes.

- [ ] **Step 4: Commit**

Run:

```powershell
git add src/WeightTracker.Web/Pages/Shared/_Layout.cshtml src/WeightTracker.Web/wwwroot/css/site.css
git commit -m "feat: add dark responsive layout"
```

## Task 9: Build History Page

**Files:**
- Create: `src/WeightTracker.Web/Pages/History.cshtml`
- Create: `src/WeightTracker.Web/Pages/History.cshtml.cs`

- [ ] **Step 1: Implement history page model**

Create `src/WeightTracker.Web/Pages/History.cshtml.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Web.Pages;

public sealed class HistoryModel(
    SettingsService settingsService,
    WeightEntryService entryService,
    MetricsService metricsService) : PageModel
{
    public string DisplayUnit { get; private set; } = "kg";
    public string Theme { get; private set; } = "dark";
    public MetricsSummary Summary { get; private set; } = new(null, null, null, null, null, null, null, null, null, null);
    public ChartSeries Chart { get; private set; } = new([], [], [], null);
    public IReadOnlyList<WeightEntry> Entries { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        DisplayUnit = settings.DisplayUnit;
        Theme = settings.Theme;
        var today = DateOnly.FromDateTime(DateTime.Today);
        Entries = await entryService.GetRangeAsync(today.AddYears(-2), today, cancellationToken);
        Summary = metricsService.BuildSummary(Entries, today, settings.WeekStartsOn, settings.GoalWeightKg);
        Chart = metricsService.BuildChartSeries(Entries, settings.WeekStartsOn, settings.GoalWeightKg);
    }
}
```

- [ ] **Step 2: Implement history markup**

Create `src/WeightTracker.Web/Pages/History.cshtml`:

```cshtml
@page
@model WeightTracker.Web.Pages.HistoryModel
@{
    ViewData["Title"] = "History";
    ViewData["Theme"] = Model.Theme;
}

<section class="chart-panel">
    <div class="section-heading">
        <h1>History</h1>
        <span>@Model.Entries.Count entries</span>
    </div>
    <canvas id="historyChart" height="320"></canvas>
</section>

<section class="metric-grid">
    <article><span>30-day change</span><strong>@FormatSigned(Model.Summary.ThirtyDayChangeKg)</strong></article>
    <article><span>90-day change</span><strong>@FormatSigned(Model.Summary.NinetyDayChangeKg)</strong></article>
    <article><span>High</span><strong>@Format(Model.Summary.RangeHighKg)</strong></article>
    <article><span>Low</span><strong>@Format(Model.Summary.RangeLowKg)</strong></article>
</section>

<section class="chart-panel">
    <div class="section-heading">
        <h2>Entries</h2>
    </div>
    <div class="table-wrap">
        <table>
            <thead>
                <tr><th>Date</th><th>Weight</th><th>Note</th></tr>
            </thead>
            <tbody>
            @foreach (var entry in Model.Entries.OrderByDescending(item => item.EntryDate))
            {
                <tr>
                    <td>@entry.EntryDate</td>
                    <td>@Format(entry.WeightKg)</td>
                    <td>@entry.Note</td>
                </tr>
            }
            </tbody>
        </table>
    </div>
</section>

@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <script>
        const daily = @Json.Serialize(Model.Chart.DailyWeights);
        const weekly = @Json.Serialize(Model.Chart.WeeklyAverages);
        const moving = @Json.Serialize(Model.Chart.MovingAverages);
        const labels = daily.map(point => point.date);
        new Chart(document.getElementById('historyChart'), {
            type: 'line',
            data: {
                labels,
                datasets: [
                    { label: 'Daily', data: daily.map(point => point.weightKg), borderColor: '#64d2ff', tension: 0.25 },
                    { label: 'Weekly avg', data: weekly.map(point => point.weightKg), borderColor: '#7bd88f', tension: 0.25 },
                    { label: '7-day avg', data: moving.map(point => point.weightKg), borderColor: '#ffd166', tension: 0.25 }
                ]
            },
            options: { responsive: true, maintainAspectRatio: false }
        });
    </script>
}

@functions {
    private string Format(decimal? valueKg)
    {
        return valueKg is null ? "-" : $"{WeightTracker.Web.Services.WeightConversionService.FromKilograms(valueKg.Value, Model.DisplayUnit):0.0} {Model.DisplayUnit}";
    }

    private string FormatSigned(decimal? valueKg)
    {
        if (valueKg is null)
        {
            return "-";
        }

        var display = WeightTracker.Web.Services.WeightConversionService.FromKilograms(valueKg.Value, Model.DisplayUnit);
        return $"{display:+0.0;-0.0;0.0} {Model.DisplayUnit}";
    }
}
```

- [ ] **Step 3: Run build and tests**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: tests pass.

- [ ] **Step 4: Commit**

Run:

```powershell
git add src/WeightTracker.Web/Pages/History.cshtml src/WeightTracker.Web/Pages/History.cshtml.cs
git commit -m "feat: add history page"
```

## Task 10: Build Settings Page

**Files:**
- Create: `src/WeightTracker.Web/Pages/Settings.cshtml`
- Create: `src/WeightTracker.Web/Pages/Settings.cshtml.cs`

- [ ] **Step 1: Implement settings page model**

Create `src/WeightTracker.Web/Pages/Settings.cshtml.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeightTracker.Web.Services;

namespace WeightTracker.Web.Pages;

public sealed class SettingsModel(SettingsService settingsService) : PageModel
{
    [BindProperty]
    public string DisplayUnit { get; set; } = "kg";

    [BindProperty]
    public decimal? GoalWeight { get; set; }

    [BindProperty]
    public DayOfWeek WeekStartsOn { get; set; } = DayOfWeek.Monday;

    [BindProperty]
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

    [BindProperty]
    public string Theme { get; set; } = "dark";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        DisplayUnit = settings.DisplayUnit;
        GoalWeight = settings.GoalWeightKg is null
            ? null
            : decimal.Round(WeightConversionService.FromKilograms(settings.GoalWeightKg.Value, settings.DisplayUnit), 1);
        WeekStartsOn = settings.WeekStartsOn;
        TimeZoneId = settings.TimeZoneId;
        Theme = settings.Theme;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var goalKg = GoalWeight is null
            ? null
            : decimal.Round(WeightConversionService.ToKilograms(GoalWeight.Value, DisplayUnit), 3);

        await settingsService.UpdateAsync(DisplayUnit, goalKg, WeekStartsOn, TimeZoneId, Theme, cancellationToken);
        return RedirectToPage();
    }
}
```

- [ ] **Step 2: Implement settings markup**

Create `src/WeightTracker.Web/Pages/Settings.cshtml`:

```cshtml
@page
@model WeightTracker.Web.Pages.SettingsModel
@{
    ViewData["Title"] = "Settings";
    ViewData["Theme"] = Model.Theme;
}

<section class="form-panel">
    <h1>Settings</h1>

    <form method="post" class="settings-form">
        <label asp-for="DisplayUnit">Display unit</label>
        <select asp-for="DisplayUnit">
            <option value="kg">kg</option>
            <option value="lb">lb</option>
        </select>

        <label asp-for="GoalWeight">Goal weight</label>
        <input asp-for="GoalWeight" type="number" inputmode="decimal" step="0.1" min="0.1" />

        <label asp-for="WeekStartsOn">Week starts on</label>
        <select asp-for="WeekStartsOn" asp-items="Html.GetEnumSelectList<DayOfWeek>()"></select>

        <label asp-for="TimeZoneId">Timezone</label>
        <input asp-for="TimeZoneId" />

        <label asp-for="Theme">Theme</label>
        <select asp-for="Theme">
            <option value="dark">Dark</option>
            <option value="light">Light</option>
            <option value="system">System</option>
        </select>

        <button type="submit">Save settings</button>
    </form>
</section>
```

- [ ] **Step 3: Run tests**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: tests pass.

- [ ] **Step 4: Commit**

Run:

```powershell
git add src/WeightTracker.Web/Pages/Settings.cshtml src/WeightTracker.Web/Pages/Settings.cshtml.cs
git commit -m "feat: add settings page"
```

## Task 11: Add Database Initialization

**Files:**
- Create: `src/WeightTracker.Web/Data/DatabaseInitializer.cs`
- Modify: `src/WeightTracker.Web/Program.cs`

- [ ] **Step 1: Add initializer**

Create `src/WeightTracker.Web/Data/DatabaseInitializer.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WeightTrackerDbContext>();
        var databaseDirectory = Path.GetDirectoryName(db.Database.GetDbConnection().DataSource);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        await db.Database.MigrateAsync();

        if (!await db.AppSettings.AnyAsync(item => item.Id == AppSettings.SingletonId))
        {
            db.AppSettings.Add(new AppSettings());
            await db.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 2: Call initializer at startup**

In `Program.cs`, before `app.Run();`, add:

```csharp
await DatabaseInitializer.InitializeAsync(app.Services);
```

Change the top-level run line to support async startup if needed. The final bottom of `Program.cs` should be:

```csharp
app.MapRazorPages();
await DatabaseInitializer.InitializeAsync(app.Services);
app.Run();

public partial class Program;
```

- [ ] **Step 3: Create migration**

Run:

```powershell
dotnet ef migrations add InitialCreate --project src/WeightTracker.Web/WeightTracker.Web.csproj
```

Expected: EF migration files are created under `src/WeightTracker.Web/Migrations`.

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/WeightTracker.Web/Data src/WeightTracker.Web/Migrations src/WeightTracker.Web/Program.cs
git commit -m "feat: initialize sqlite database"
```

## Task 12: Add Docker Compose Deployment

**Files:**
- Create: `src/WeightTracker.Web/Dockerfile`
- Create: `docker-compose.yml`
- Modify: `README.md`

- [ ] **Step 1: Add Dockerfile**

Create `src/WeightTracker.Web/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WeightTracker.sln ./
COPY Directory.Build.props ./
COPY src/WeightTracker.Web/WeightTracker.Web.csproj src/WeightTracker.Web/
COPY tests/WeightTracker.Tests/WeightTracker.Tests.csproj tests/WeightTracker.Tests/
RUN dotnet restore WeightTracker.sln

COPY . .
RUN dotnet publish src/WeightTracker.Web/WeightTracker.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__WeightTracker=Data Source=/app/data/weighttracker.db

RUN mkdir -p /app/data
VOLUME ["/app/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "WeightTracker.Web.dll"]
```

- [ ] **Step 2: Add Compose file**

Create `docker-compose.yml`:

```yaml
services:
  weighttracker:
    build:
      context: .
      dockerfile: src/WeightTracker.Web/Dockerfile
    container_name: weighttracker
    ports:
      - "8080:8080"
    environment:
      TZ: Europe/London
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__WeightTracker: Data Source=/app/data/weighttracker.db
    volumes:
      - ./data:/app/data
    restart: unless-stopped
```

- [ ] **Step 3: Update README**

Update `README.md` with:

```markdown
## Running Locally

Run the app directly:

```powershell
dotnet run --project src/WeightTracker.Web/WeightTracker.Web.csproj
```

Run tests:

```powershell
dotnet test WeightTracker.sln
```

Run with Docker Compose:

```powershell
docker compose up --build
```

The app listens on `http://localhost:8080` in Docker Compose. SQLite data is stored in `./data`, which is ignored by git.
```

- [ ] **Step 4: Validate build and Docker config**

Run:

```powershell
dotnet test WeightTracker.sln
docker compose config
```

Expected: tests pass. Docker command passes on a machine with Docker installed.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/WeightTracker.Web/Dockerfile docker-compose.yml README.md
git commit -m "feat: add docker compose deployment"
```

## Task 13: Final Verification

**Files:**
- Modify: only files needed to fix verification failures.

- [ ] **Step 1: Run full test suite**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: all tests pass.

- [ ] **Step 2: Run web app locally**

Run:

```powershell
dotnet run --project src/WeightTracker.Web/WeightTracker.Web.csproj
```

Expected: app starts and reports a localhost URL.

- [ ] **Step 3: Manually check pages**

Open the reported URL and verify:

- `/` shows today's weight input at the top.
- Saving a valid weight creates today's entry.
- Saving a second value for today updates the displayed value.
- Weekly metrics render without errors.
- `/history` renders chart area and entries table.
- `/settings` saves theme and display unit.

- [ ] **Step 4: Run Docker verification where Docker is available**

Run:

```powershell
docker compose up --build
```

Expected: container starts and app is reachable at `http://localhost:8080`.

- [ ] **Step 5: Commit final fixes**

If verification required fixes, run:

```powershell
git add .
git commit -m "fix: complete initial app verification"
```

If no fixes were needed, do not create an empty commit.

## Self-Review Notes

Spec coverage:

- Daily single-user entry is covered by Tasks 5 and 7.
- One entry per date is covered by Task 5 and the unique index in Task 2.
- Weekly average comparison is covered by Task 6 and displayed in Task 7.
- Dark mode default and saved theme are covered by Tasks 4, 8, and 10.
- History graphs and metrics are covered by Tasks 6 and 9.
- Optional goal weight is covered by Tasks 4, 6, 7, 9, and 10.
- Docker Compose deployment is covered by Task 12.
- Auth extension path is preserved through service boundaries and no page-level persistence logic.

Verification limits:

- Docker CLI is not currently available on this workstation. Docker validation must run on a machine with Docker installed or after Docker is added to `PATH`.

