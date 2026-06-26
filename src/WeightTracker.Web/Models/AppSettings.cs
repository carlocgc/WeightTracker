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
