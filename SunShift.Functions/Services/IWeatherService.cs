using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

public interface IWeatherService
{
    Task<IReadOnlyList<WeatherData>> FetchForecastAsync(
        int locationId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
