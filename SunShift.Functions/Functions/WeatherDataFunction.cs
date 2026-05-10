using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Functions.Services;

namespace SunShift.Functions.Functions;

public class WeatherDataFunction(
    ILocationRepository locationRepository,
    IWeatherDataRepository weatherDataRepository,
    IWeatherService weatherService,
    IUnitOfWork unitOfWork,
    ILogger<WeatherDataFunction> logger)
{
    // Runs daily at 18:30 UTC  →  cron: second minute hour day month day-of-week
    [Function(nameof(WeatherDataFunction))]
    public async Task RunAsync([TimerTrigger("0 30 18 * * *")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "WeatherDataFunction triggered at {Time:O}. IsPastDue: {IsPastDue}",
            DateTime.UtcNow, timerInfo.IsPastDue);

        IReadOnlyList<Location> locations = await locationRepository.GetAllAsync(cancellationToken);

        if (locations.Count == 0)
        {
            logger.LogInformation("No active locations found. Skipping weather data fetch.");
            return;
        }

        logger.LogInformation("Fetching 7-day forecast for {Count} location(s).", locations.Count);

        foreach (Location location in locations)
        {
            try
            {
                IReadOnlyList<WeatherData> forecast = await weatherService.FetchForecastAsync(
                    location.Id, location.Latitude, location.Longitude, cancellationToken);

                await weatherDataRepository.UpsertRangeAsync(forecast, cancellationToken);
                _ = await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Upserted {Count} weather records for location '{Name}' (Id={Id}).",
                    forecast.Count, location.Name, location.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to fetch or upsert weather data for location '{Name}' (Id={Id}).",
                    location.Name, location.Id);
            }
        }

        logger.LogInformation("WeatherDataFunction completed.");
    }
}
