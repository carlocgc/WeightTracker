using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class WeightConversionServiceTests
{
    [Fact]
    public void ToKilograms_WithKilograms_ReturnsInput()
    {
        var result = WeightConversionService.ToKilograms(82.4m, "kg");

        Assert.Equal(82.4m, result);
    }

    [Fact]
    public void ToKilograms_WithPounds_ConvertsToKilograms()
    {
        var result = WeightConversionService.ToKilograms(200m, "lb");

        Assert.Equal(90.718m, decimal.Round(result, 3));
    }

    [Fact]
    public void FromKilograms_WithPounds_ConvertsToPounds()
    {
        var result = WeightConversionService.FromKilograms(90.718474m, "lb");

        Assert.Equal(200.000m, decimal.Round(result, 3));
    }

    [Fact]
    public void ToKilograms_WithUnsupportedUnit_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeightConversionService.ToKilograms(82.4m, "stone"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ToKilograms_WithNonPositiveValue_ThrowsArgumentOutOfRangeException(decimal value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeightConversionService.ToKilograms(value, "kg"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromKilograms_WithNonPositiveValue_ThrowsArgumentOutOfRangeException(decimal value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeightConversionService.FromKilograms(value, "lb"));
    }

    [Fact]
    public void Conversion_NormalizesWhitespaceAndCaseInUnits()
    {
        var kilograms = WeightConversionService.ToKilograms(82.4m, " KG ");
        var pounds = WeightConversionService.FromKilograms(90.718474m, " LB ");

        Assert.Equal(82.4m, kilograms);
        Assert.Equal(200.000m, decimal.Round(pounds, 3));
    }

    [Fact]
    public void ToKilograms_WithNullUnit_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => WeightConversionService.ToKilograms(82.4m, null!));
    }
}
