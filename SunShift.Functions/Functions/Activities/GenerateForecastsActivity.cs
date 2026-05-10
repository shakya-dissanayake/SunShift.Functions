using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
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
        DateTime slFrom = DateTime.Now.Date;  // SL local (WEBSITE_TIME_ZONE = Sri Lanka Standard Time)
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

        // Group users by location to load weather data only once per location
        IEnumerable<IGrouping<int, User>> byLocation = users
            .Where(u => u.SolarProfile is not null)
            .GroupBy(u => u.LocationId);

        foreach (IGrouping<int, User> group in byLocation)
        {
            int locationId = group.Key;
            Dictionary<DateTime, WeatherData> weatherMap =
                await LoadWeatherMapAsync(locationId, slFrom, slTo, cancellationToken);

            foreach (User user in group)
            {
                await GenerateForUserAsync(user, weatherMap, slFrom, slTo, cancellationToken);
            }
        }

        logger.LogInformation("GenerateForecastsActivity: completed.");
    }

    // ── Called for a single user on-demand (e.g. from queue trigger) ──────

    public async Task GenerateForUserAsync(
        User user,
        Dictionary<DateTime, WeatherData> weatherMap,
        DateTime slFrom,
        DateTime slTo,
        CancellationToken cancellationToken)
    {
        try
        {
            var dailyResults = await solarForecastService.GenerateAsync(
                user, weatherMap, slFrom, slTo, cancellationToken);

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
        int locationId,
        DateTime slFrom,
        DateTime slTo,
        CancellationToken cancellationToken)
    {
        // Load 2h before slFrom to supply lag/rolling features at the first interval
        DateTime from = slFrom.AddHours(-2);
        DateTime to = slTo.Date.AddHours(23).AddMinutes(45);

        List<WeatherData> rows = await weatherDataRepository.GetByLocationAndRangeAsync(
            locationId, from, to, cancellationToken);

        return rows.ToDictionary(w => w.Timestamp);
    }
}
