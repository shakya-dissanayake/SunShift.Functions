using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Domain.Common;
using SunShift.Domain.Entities;
using SunShift.Functions.Functions.Activities;

namespace SunShift.Functions.Functions;

public class OnDemandForecastFunction(
    IUserRepository userRepository,
    GenerateForecastsActivity generateForecastsActivity,
    IProgressNotifier progressNotifier,
    ILogger<OnDemandForecastFunction> logger)
{
    private sealed record ForecastRequest(int UserId);

    [Function(nameof(OnDemandForecastFunction))]
    public async Task RunAsync(
        [QueueTrigger("forecast-requests")] string messageJson,
        CancellationToken cancellationToken)
    {
        ForecastRequest? request = JsonSerializer.Deserialize<ForecastRequest>(
            messageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (request is null || request.UserId <= 0)
        {
            logger.LogWarning("OnDemandForecastFunction: invalid message received: {Message}", messageJson);
            return;
        }

        int userId = request.UserId;

        logger.LogInformation(
            "OnDemandForecastFunction: generating forecasts on demand for userId={UserId}.", userId);

        await progressNotifier.NotifyAsync(userId, "FetchingUser", 5, cancellationToken);

        User? user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("OnDemandForecastFunction: user {UserId} not found.", userId);
            await progressNotifier.NotifyAsync(userId, "Failed", 0, cancellationToken);
            return;
        }

        if (user.SolarProfile is null)
        {
            logger.LogWarning("OnDemandForecastFunction: user {UserId} has no SolarProfile.", userId);
            await progressNotifier.NotifyAsync(userId, "Failed", 0, cancellationToken);
            return;
        }

        DateTime slFrom = DateTime.Now.Date;  // SL local (WEBSITE_TIME_ZONE = Sri Lanka Standard Time)
        DateTime slTo = slFrom.AddDays(13);
        int totalDays = (int)(slTo - slFrom).TotalDays + 1;

        // --- Phase 1: Preparing weather data (0..100 over the day horizon) ---
        string geoKey = GeoKey.From(user.Latitude, user.Longitude);
        Dictionary<DateTime, WeatherData> weatherMap = await generateForecastsActivity
            .LoadWeatherMapAsync(geoKey, slFrom, slTo, cancellationToken);

        await progressNotifier.NotifyAsync(userId, "PreparingWeatherData", 0, cancellationToken);
        for (int i = 0; i < totalDays; i++)
        {
            // Validate weather coverage for each day so the phase reflects real per-day work.
            DateTime day = slFrom.AddDays(i);
            bool hasWeather = weatherMap.Keys.Any(k => k.Date == day);
            if (!hasWeather)
            {
                logger.LogDebug(
                    "OnDemandForecastFunction: no weather for user {UserId} on {Date:yyyy-MM-dd}.",
                    userId, day);
            }

            int percent = (int)Math.Round((double)(i + 1) / totalDays * 100);
            await progressNotifier.NotifyAsync(userId, "PreparingWeatherData", percent, cancellationToken);
        }

        // --- Phase 2: Forecasting solar generation (0..100 over the day horizon) ---
        await progressNotifier.NotifyAsync(userId, "ForecastingSolarGeneration", 0, cancellationToken);
        await generateForecastsActivity.GenerateForUserAsync(
            user, weatherMap, slFrom, slTo, cancellationToken,
            onDayProgress: (daysProcessed, dayTotal) =>
            {
                int percent = (int)Math.Round((double)daysProcessed / dayTotal * 100);
                return progressNotifier.NotifyAsync(
                    userId, "ForecastingSolarGeneration", percent, cancellationToken);
            });

        await progressNotifier.NotifyAsync(userId, "Ready", 100, cancellationToken);

        logger.LogInformation("OnDemandForecastFunction: completed for userId={UserId}.", userId);
    }
}
