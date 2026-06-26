namespace WeightTracker.Web.Services;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The clock time must be UTC.", nameof(utcNow));
        }

        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}
