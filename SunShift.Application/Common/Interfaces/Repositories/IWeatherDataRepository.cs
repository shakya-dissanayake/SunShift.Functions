using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface IWeatherDataRepository
{
    Task UpsertRangeAsync(IEnumerable<WeatherData> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns weather data for a location within a UTC timestamp range (inclusive),
    /// ordered by Timestamp ascending.
    /// </summary>
    Task<List<WeatherData>> GetByLocationAndRangeAsync(int locationId, DateTime utcFrom, DateTime utcTo, CancellationToken cancellationToken = default);
}
