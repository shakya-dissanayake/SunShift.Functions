using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Common;
using SunShift.Domain.Entities;
using SunShift.Functions.Services;

namespace SunShift.Functions.Functions.Activities;

public class FetchWeatherActivity(
    IUserRepository userRepository,
    IWeatherDataRepository weatherDataRepository,
    IWeatherService weatherService,
    IUnitOfWork unitOfWork,
    ILogger<FetchWeatherActivity> logger)
{
    [Function(nameof(FetchWeatherActivity))]
    public async Task RunAsync([ActivityTrigger] object? input, CancellationToken cancellationToken)
    {
        List<(double Latitude, double Longitude)> coordinates =
            await userRepository.GetDistinctCoordinatesAsync(cancellationToken);

        if (coordinates.Count == 0)
        {
            logger.LogInformation("FetchWeatherActivity: no user coordinates found.");
            return;
        }

        // Collapse nearby users onto a shared weather grid so we fetch once per grid cell,
        // not once per user. The first coordinate seen for a key is its representative point.
        Dictionary<string, (double Latitude, double Longitude)> byGeoKey = new();
        foreach ((double lat, double lon) in coordinates)
        {
            string key = GeoKey.From(lat, lon);
            _ = byGeoKey.TryAdd(key, (lat, lon));
        }

        logger.LogInformation(
            "FetchWeatherActivity: fetching 16-day forecast for {Cells} weather-grid cell(s) from {Users} user coordinate(s).",
            byGeoKey.Count, coordinates.Count);

        foreach ((string geoKey, (double lat, double lon)) in byGeoKey)
        {
            try
            {
                IReadOnlyList<WeatherData> forecast = await weatherService.FetchForecastAsync(
                    geoKey, lat, lon, cancellationToken);

                await weatherDataRepository.UpsertRangeAsync(forecast, cancellationToken);
                _ = await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "FetchWeatherActivity: upserted {Count} records for grid cell {GeoKey} ({Lat}, {Lon}).",
                    forecast.Count, geoKey, lat, lon);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "FetchWeatherActivity: failed for grid cell {GeoKey} ({Lat}, {Lon}).",
                    geoKey, lat, lon);
            }
        }
    }
}
