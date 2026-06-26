using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Data;

public sealed class WeightTrackerDbContext(DbContextOptions<WeightTrackerDbContext> options) : DbContext(options)
{
    public DbSet<WeightEntry> WeightEntries { get; set; }

    public DbSet<AppSettings> AppSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeightEntry>(entity =>
        {
            entity.HasIndex(weightEntry => weightEntry.EntryDate).IsUnique();
            entity.Property(weightEntry => weightEntry.WeightKg).HasPrecision(8, 3);
            entity.Property(weightEntry => weightEntry.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.Property(settings => settings.DisplayUnit).HasMaxLength(2);
            entity.Property(settings => settings.GoalWeightKg).HasPrecision(8, 3);
            entity.Property(settings => settings.TimeZoneId).HasMaxLength(100);
            entity.Property(settings => settings.Theme).HasMaxLength(10);
        });
    }
}
