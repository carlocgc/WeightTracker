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
