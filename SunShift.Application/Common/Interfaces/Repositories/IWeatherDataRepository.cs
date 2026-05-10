using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface IWeatherDataRepository
{
    Task UpsertRangeAsync(IEnumerable<WeatherData> items, CancellationToken cancellationToken = default);
}
