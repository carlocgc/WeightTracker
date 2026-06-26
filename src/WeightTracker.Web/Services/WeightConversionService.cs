namespace WeightTracker.Web.Services;

public static class WeightConversionService
{
    private const decimal PoundsPerKilogram = 2.20462262185m;

    public static decimal ToKilograms(decimal value, string unit)
    {
        ValidatePositiveValue(value);

        return NormalizeUnit(unit) switch
        {
            "kg" => value,
            "lb" => value / PoundsPerKilogram,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Supported units are kg and lb."),
        };
    }

    public static decimal FromKilograms(decimal value, string unit)
    {
        ValidatePositiveValue(value);

        return NormalizeUnit(unit) switch
        {
            "kg" => value,
            "lb" => value * PoundsPerKilogram,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Supported units are kg and lb."),
        };
    }

    private static void ValidatePositiveValue(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Weight must be greater than zero.");
        }
    }

    public static string NormalizeUnit(string unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return unit.Trim().ToLowerInvariant();
    }
}
