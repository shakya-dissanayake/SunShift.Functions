using SunShift.Domain.Common;

namespace SunShift.Functions.Tests;

public class GeoKeyTests
{
    [Fact]
    public void RoundsToTwoDecimals()
    {
        // 6.88951 and 79.89552 round to 6.89 / 79.90.
        Assert.Equal("6.89_79.90", GeoKey.From(6.88951, 79.89552));
    }

    [Fact]
    public void NearbyCoordinates_ShareTheSameKey()
    {
        // Two users ~50 m apart fall in the same ~1.1 km grid cell → one weather pull.
        string a = GeoKey.From(6.8895, 79.8955);
        string b = GeoKey.From(6.8897, 79.8953);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DistinctCoordinates_GetDistinctKeys()
    {
        string colombo = GeoKey.From(6.93, 79.84);
        string kandy = GeoKey.From(7.29, 80.63);
        Assert.NotEqual(colombo, kandy);
    }

    [Fact]
    public void IsCultureInvariant_UsesDotDecimalSeparator()
    {
        // Must always use '.' regardless of the host locale (the value is stored/compared as text).
        Assert.Contains(".", GeoKey.From(6.89, 79.90));
        Assert.DoesNotContain(",", GeoKey.From(6.89, 79.90));
    }

    [Fact]
    public void HandlesNegativeCoordinates()
    {
        Assert.Equal("-33.87_151.21", GeoKey.From(-33.8688, 151.2093));
    }
}
