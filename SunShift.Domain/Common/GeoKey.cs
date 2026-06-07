using System.Globalization;

namespace SunShift.Domain.Common;

/// <summary>
/// Derives the weather-grid key for a coordinate. Coordinates are rounded to 2 decimal
/// places (~1.1 km, close to Open-Meteo's model grid) so nearby users share one weather
/// pull instead of triggering an API call each.
/// </summary>
public static class GeoKey
{
    public const int Decimals = 2;

    public static string From(double latitude, double longitude)
    {
        double lat = Math.Round(latitude, Decimals);
        double lon = Math.Round(longitude, Decimals);
        return string.Create(CultureInfo.InvariantCulture, $"{lat:F2}_{lon:F2}");
    }
}
