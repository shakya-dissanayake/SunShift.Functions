using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Functions.Services;

namespace SunShift.Functions.Functions.Activities;

public class FetchWeatherActivity(
    ILocationRepository locationRepository,
    IWeatherDataRepository weatherDataRepository,
    IWeatherService weatherService,
    IUnitOfWork unitOfWork,
    ILogger<FetchWeatherActivity> logger)
{
    [Function(nameof(FetchWeatherActivity))]
    public async Task RunAsync([ActivityTrigger] object? input, CancellationToken cancellationToken)
    {
        IReadOnlyList<Location> locations = await locationRepository.GetAllAsync(cancellationToken);

        if (locations.Count == 0)
        {
            logger.LogInformation("FetchWeatherActivity: no active locations found.");
            return;
        }

        logger.LogInformation("FetchWeatherActivity: fetching 16-day forecast for {Count} location(s).", locations.Count);

        foreach (Location location in locations)
        {
            try
            {
                IReadOnlyList<WeatherData> forecast = await weatherService.FetchForecastAsync(
                    location.Id, location.Latitude, location.Longitude, cancellationToken);

                await weatherDataRepository.UpsertRangeAsync(forecast, cancellationToken);
                _ = await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "FetchWeatherActivity: upserted {Count} records for '{Name}' (Id={Id}).",
                    forecast.Count, location.Name, location.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "FetchWeatherActivity: failed for location '{Name}' (Id={Id}).",
                    location.Name, location.Id);
            }
        }
    }
}
