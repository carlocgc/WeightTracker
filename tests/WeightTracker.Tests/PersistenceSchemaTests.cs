using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Models;

namespace WeightTracker.Tests;

public sealed class PersistenceSchemaTests
{
    [Fact]
    public void ValidWeightEntry_WithNullNote_PersistsAndRoundTrips()
    {
        using var fixture = new ServiceTestFixture();
        var entryDate = new DateOnly(2026, 6, 26);

        using (var db = fixture.CreateDbContext())
        {
            db.WeightEntries.Add(new WeightEntry
            {
                EntryDate = entryDate,
                WeightKg = 82.125m,
                Note = null,
            });

            db.SaveChanges();
        }

        using var readDb = fixture.CreateDbContext();
        var entry = readDb.WeightEntries.Single();

        Assert.Equal(entryDate, entry.EntryDate);
        Assert.Equal(82.125m, entry.WeightKg);
        Assert.Null(entry.Note);
    }

    [Fact]
    public void DuplicateEntryDate_IsRejectedBySqlite()
    {
        using var fixture = new ServiceTestFixture();
        var entryDate = new DateOnly(2026, 6, 26);

        using (var db = fixture.CreateDbContext())
        {
            db.WeightEntries.Add(new WeightEntry
            {
                EntryDate = entryDate,
                WeightKg = 82.125m,
            });
            db.SaveChanges();
        }

        using var duplicateDb = fixture.CreateDbContext();
        duplicateDb.WeightEntries.Add(new WeightEntry
        {
            EntryDate = entryDate,
            WeightKg = 82.126m,
        });

        Assert.Throws<DbUpdateException>(() => duplicateDb.SaveChanges());
    }

    [Theory]
    [InlineData("0.099")]
    [InlineData("1000.001")]
    [InlineData("82.1234")]
    public void InvalidWeightKg_IsRejectedBySqlite(string weightKg)
    {
        using var fixture = new ServiceTestFixture();
        using var db = fixture.CreateDbContext();
        db.WeightEntries.Add(new WeightEntry
        {
            EntryDate = new DateOnly(2026, 6, 26),
            WeightKg = decimal.Parse(weightKg, CultureInfo.InvariantCulture),
        });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
