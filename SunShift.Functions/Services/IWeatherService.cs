using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

public interface IWeatherService
{
    Task<IReadOnlyList<WeatherData>> FetchForecastAsync(
        string geoKey,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
