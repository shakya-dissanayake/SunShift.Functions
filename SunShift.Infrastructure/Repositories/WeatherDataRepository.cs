using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class WeatherDataRepository(ApplicationDbContext context) : IWeatherDataRepository
{
    public async Task UpsertRangeAsync(IEnumerable<WeatherData> items, CancellationToken cancellationToken = default)
    {
        List<WeatherData> itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return;
        }

        List<int> locationIds = itemList.Select(x => x.LocationId).Distinct().ToList();
        DateTime minTs = itemList.Min(x => x.Timestamp);
        DateTime maxTs = itemList.Max(x => x.Timestamp);

        // IgnoreQueryFilters so we can find and restore soft-deleted rows
        List<WeatherData> existing = await context.WeatherData
            .IgnoreQueryFilters()
            .Where(x => locationIds.Contains(x.LocationId)
                     && x.Timestamp >= minTs
                     && x.Timestamp <= maxTs)
            .ToListAsync(cancellationToken);

        Dictionary<(int LocationId, DateTime Timestamp), WeatherData> existingLookup = existing.ToDictionary(x => (x.LocationId, x.Timestamp));

        foreach (WeatherData item in itemList)
        {
            if (existingLookup.TryGetValue((item.LocationId, item.Timestamp), out WeatherData? existingRow))
            {
                MapProperties(item, existingRow);
                existingRow.IsDeleted = false;
            }
            else
            {
                _ = context.WeatherData.Add(item);
            }
        }
    }

    private static void MapProperties(WeatherData src, WeatherData dest)
    {
        dest.ShortwaveRadiation = src.ShortwaveRadiation;
        dest.DirectRadiation = src.DirectRadiation;
        dest.DiffuseRadiation = src.DiffuseRadiation;
        dest.DirectNormalIrradiance = src.DirectNormalIrradiance;
        dest.CloudCover = src.CloudCover;
        dest.CloudCoverLow = src.CloudCoverLow;
        dest.CloudCoverMid = src.CloudCoverMid;
        dest.CloudCoverHigh = src.CloudCoverHigh;
        dest.Temperature = src.Temperature;
        dest.RelativeHumidity = src.RelativeHumidity;
        dest.DewPoint = src.DewPoint;
        dest.Precipitation = src.Precipitation;
        dest.Rain = src.Rain;
        dest.WindSpeed = src.WindSpeed;
        dest.WindDirection = src.WindDirection;
        dest.SurfacePressure = src.SurfacePressure;
        dest.SunshineDuration = src.SunshineDuration;
        dest.IsDay = src.IsDay;
    }
}
