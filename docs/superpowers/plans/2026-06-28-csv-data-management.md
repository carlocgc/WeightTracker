# CSV Data Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add dashboard CSV export, CSV import, and guarded delete-all behavior for weight entries only.

**Architecture:** Add a focused `WeightDataService` that owns CSV parsing, CSV writing, import upserts, and delete-all persistence. Keep Razor Page handlers responsible for HTTP binding, file results, model errors, redirects, and dashboard status messages.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQLite, xUnit, invariant-culture CSV parsing/writing implemented with the .NET base class library.

---

### Task 1: CSV Export Service

**Files:**
- Create: `src/WeightTracker.Web/Services/WeightDataService.cs`
- Test: `tests/WeightTracker.Tests/WeightDataServiceTests.cs`

- [ ] **Step 1: Write failing export tests**

Add tests that seed unsorted entries, call `ExportCsvAsync`, and assert the header, date ordering, kg formatting, empty notes, and escaping for commas, quotes, and line breaks.

```csharp
[Fact]
public async Task ExportCsvAsync_WritesWeightEntriesInDateOrder()
{
    using var fixture = new ServiceTestFixture();
    await using var db = fixture.CreateDbContext();
    await AddEntryAsync(db, new DateOnly(2026, 6, 26), 82.1m, null);
    await AddEntryAsync(db, new DateOnly(2026, 6, 24), 83.125m, "first");
    var service = CreateService(db);

    var csv = await service.ExportCsvAsync();

    Assert.Equal(
        "entry_date,weight_kg,note\n2026-06-24,83.125,first\n2026-06-26,82.100,\n",
        csv);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter WeightDataServiceTests --logger "console;verbosity=normal"`

Expected: compile failure because `WeightDataService` does not exist.

- [ ] **Step 3: Implement minimal export service**

Create `WeightDataService` with:

```csharp
public sealed class WeightDataService(WeightTrackerDbContext db, ILocalDateProvider localDateProvider, IClock clock)
{
    public async Task<string> ExportCsvAsync(CancellationToken cancellationToken = default)
    {
        var entries = await db.WeightEntries.AsNoTracking().OrderBy(entry => entry.EntryDate).ToListAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.Append("entry_date,weight_kg,note\n");
        foreach (var entry in entries)
        {
            builder.Append(entry.EntryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(entry.WeightKg.ToString("0.000", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(EscapeCsv(entry.Note ?? string.Empty));
            builder.Append('\n');
        }
        return builder.ToString();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter WeightDataServiceTests --logger "console;verbosity=normal"`

Expected: export tests pass.

### Task 2: CSV Import Service

**Files:**
- Modify: `src/WeightTracker.Web/Services/WeightDataService.cs`
- Modify: `tests/WeightTracker.Tests/WeightDataServiceTests.cs`

- [ ] **Step 1: Write failing import tests**

Add tests for valid insert, valid update, duplicate CSV dates, future dates, missing headers, invalid weights, over-precision weights, long notes, and all-or-nothing persistence.

```csharp
[Fact]
public async Task ImportCsvAsync_UpsertsByDate()
{
    using var fixture = new ServiceTestFixture();
    await using var db = fixture.CreateDbContext();
    await AddEntryAsync(db, new DateOnly(2026, 6, 24), 90m, "old");
    var service = CreateService(db);

    var result = await service.ImportCsvAsync("entry_date,weight_kg,note\n2026-06-24,82.125,new\n2026-06-25,81.000,\n");

    Assert.True(result.Success);
    Assert.Equal(1, result.InsertedCount);
    Assert.Equal(1, result.UpdatedCount);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter WeightDataServiceTests --logger "console;verbosity=normal"`

Expected: compile failure because `ImportCsvAsync` does not exist.

- [ ] **Step 3: Implement import result types, parser, validation, and upsert**

Add:

```csharp
public sealed record WeightDataImportResult(bool Success, int InsertedCount, int UpdatedCount, IReadOnlyList<string> Errors)
{
    public static WeightDataImportResult Failed(IReadOnlyList<string> errors) => new(false, 0, 0, errors);
    public static WeightDataImportResult Imported(int insertedCount, int updatedCount) => new(true, insertedCount, updatedCount, []);
}
```

Implement `ImportCsvAsync` to parse all rows, reject invalid files before saving, reject future dates by `ILocalDateProvider`, reject duplicate dates in the file, and upsert valid rows in one save.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter WeightDataServiceTests --logger "console;verbosity=normal"`

Expected: import tests pass.

### Task 3: Delete-All Service

**Files:**
- Modify: `src/WeightTracker.Web/Services/WeightDataService.cs`
- Modify: `tests/WeightTracker.Tests/WeightDataServiceTests.cs`

- [ ] **Step 1: Write failing delete-all tests**

Add tests that incorrect confirmation refuses deletion and exact `DELETE` deletes all entries while preserving settings.

```csharp
[Fact]
public async Task DeleteAllWeightsAsync_RequiresExactDeleteConfirmation()
{
    using var fixture = new ServiceTestFixture();
    await using var db = fixture.CreateDbContext();
    await AddEntryAsync(db, new DateOnly(2026, 6, 24), 82m, null);
    var service = CreateService(db);

    var result = await service.DeleteAllWeightsAsync("delete");

    Assert.False(result.Success);
    Assert.Single(await db.WeightEntries.ToListAsync());
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter WeightDataServiceTests --logger "console;verbosity=normal"`

Expected: compile failure because `DeleteAllWeightsAsync` does not exist.

- [ ] **Step 3: Implement delete-all**

Add a method that returns a result with success, deleted count, and errors. It must require exact `DELETE`, remove only `WeightEntries`, and leave `AppSettings` rows unchanged.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter WeightDataServiceTests --logger "console;verbosity=normal"`

Expected: delete-all tests pass.

### Task 4: Dashboard Wiring And Docs

**Files:**
- Modify: `src/WeightTracker.Web/Program.cs`
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml.cs`
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`
- Modify: `README.md`
- Modify: `docs/ROADMAP.md`

- [ ] **Step 1: Write failing dashboard tests**

Add tests that the dashboard renders the Data section, export returns a CSV download, valid import redirects and persists, invalid import returns validation without partial writes, delete-all requires confirmation, and valid delete-all removes entries while preserving settings.

- [ ] **Step 2: Run dashboard tests to verify they fail**

Run: `dotnet test .\tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter DashboardPageTests --logger "console;verbosity=normal"`

Expected: compile failures or missing UI/handler failures.

- [ ] **Step 3: Wire service and page handlers**

Register `WeightDataService` in `Program.cs`. Inject it into `IndexModel`. Add `ImportFile`, `DeleteAllConfirmation`, and `StatusMessage` properties. Add `OnGetExportCsvAsync`, `OnPostImportCsvAsync`, and `OnPostDeleteAllWeightsAsync` handlers.

- [ ] **Step 4: Add dashboard Data UI**

Add a Data section after Insights with export, import, and delete-all actions. Add a delete warning dialog and a typed confirmation dialog. Add small JavaScript handlers to open/close the dialogs and disable the destructive submit until the typed value is exact `DELETE`.

- [ ] **Step 5: Add focused CSS**

Extend existing dashboard styling for the data section, upload input, status message, and destructive confirmation. Keep the mobile-first 430px layout intact.

- [ ] **Step 6: Update README and ROADMAP**

Document CSV backup/migration commands through the UI. Revise the roadmap CSV item so it no longer says settings import/export is part of this feature.

- [ ] **Step 7: Run full verification**

Run: `dotnet test .\tests\WeightTracker.Tests\WeightTracker.Tests.csproj --logger "console;verbosity=normal"`

Expected: all tests pass.
