using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface IWeatherDataRepository
{
    Task UpsertRangeAsync(IEnumerable<WeatherData> items, CancellationToken cancellationToken = default);
    Task<List<WeatherData>> GetByGeoKeyAndRangeAsync(string geoKey, DateTime slFrom, DateTime slTo, CancellationToken cancellationToken = default);
}
