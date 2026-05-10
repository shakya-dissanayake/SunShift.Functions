using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

public interface ISolarForecastService
{
    /// <summary>
    /// Generates Forecast + ForecastInterval entities for the given user across a range of
    /// Sri Lanka local dates.  Days with no weather data in the DB are silently skipped.
    /// Returns one Forecast per day that has at least one weather reading.
    /// weatherBySl: keyed by SL local timestamp (DateTime.Kind = Unspecified).
    /// </summary>
    Task<List<(DateTime TargetDateSl, double TotalKwh, List<ForecastInterval> Intervals)>> GenerateAsync(
        User user,
        Dictionary<DateTime, WeatherData> weatherBySl,
        DateTime slFromDate,
        DateTime slToDate,
        CancellationToken cancellationToken = default);
}
