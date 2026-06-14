using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Common;
using SunShift.Domain.Entities;
using SunShift.Functions.Services;

namespace SunShift.Functions.Functions.Activities;

public class GenerateForecastsActivity(
    IUserRepository userRepository,
    IWeatherDataRepository weatherDataRepository,
    IForecastRepository forecastRepository,
    IUnitOfWork unitOfWork,
    ISolarForecastService solarForecastService,
    ILogger<GenerateForecastsActivity> logger)
{

    [Function(nameof(GenerateForecastsActivity))]
    public async Task RunAsync([ActivityTrigger] object? input, CancellationToken cancellationToken)
    {
        DateTime slFrom = SriLankaTime.Today;
        DateTime slTo = slFrom.AddDays(13);

        List<User> users = await userRepository.GetAllActiveWithProfilesAsync(cancellationToken);

        if (users.Count == 0)
        {
            logger.LogInformation("GenerateForecastsActivity: no active users found.");
            return;
        }

        logger.LogInformation(
            "GenerateForecastsActivity: generating forecasts for {Count} user(s), SL range {From:yyyy-MM-dd} to {To:yyyy-MM-dd}.",
            users.Count, slFrom, slTo);

        // Group users by weather-grid key to load weather only once per grid cell.
        IEnumerable<IGrouping<string, User>> byGeoKey = users
            .Where(u => u.SolarProfile is not null)
            .GroupBy(u => GeoKey.From(u.Latitude, u.Longitude));

        foreach (IGrouping<string, User> group in byGeoKey)
        {
            string geoKey = group.Key;
            Dictionary<DateTime, WeatherData> weatherMap =
                await LoadWeatherMapAsync(geoKey, slFrom, slTo, cancellationToken);

            foreach (User user in group)
            {
                await GenerateForUserAsync(user, weatherMap, slFrom, slTo, cancellationToken);
            }
        }

        logger.LogInformation("GenerateForecastsActivity: completed.");
    }

    // Called for a single user on-demand (e.g. from queue trigger)
    public async Task GenerateForUserAsync(
        User user,
        Dictionary<DateTime, WeatherData> weatherMap,
        DateTime slFrom,
        DateTime slTo,
        CancellationToken cancellationToken,
        Func<int, int, Task>? onDayProgress = null)
    {
        try
        {
            var dailyResults = await solarForecastService.GenerateAsync(
                user, weatherMap, slFrom, slTo, cancellationToken, onDayProgress);

            foreach (var (targetDateSl, totalKwh, intervals) in dailyResults)
            {
                await forecastRepository.UpsertAsync(
                    user.Id, targetDateSl, totalKwh, intervals, cancellationToken);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "GenerateForecastsActivity: generated {Days} day(s) of forecasts for user {UserId}.",
                dailyResults.Count, user.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "GenerateForecastsActivity: failed for user {UserId}.", user.Id);
        }
    }

    internal async Task<Dictionary<DateTime, WeatherData>> LoadWeatherMapAsync(
        string geoKey,
        DateTime slFrom,
        DateTime slTo,
        CancellationToken cancellationToken)
    {
        // Load 2h before slFrom to supply lag/rolling features at the first interval
        DateTime from = slFrom.AddHours(-2);
        DateTime to = slTo.Date.AddHours(23).AddMinutes(45);

        List<WeatherData> rows = await weatherDataRepository.GetByGeoKeyAndRangeAsync(
            geoKey, from, to, cancellationToken);

        return rows.ToDictionary(w => w.Timestamp);
    }
}
