using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Functions.Functions.Activities;

namespace SunShift.Functions.Functions;

public class OnDemandForecastFunction(
    IUserRepository userRepository,
    GenerateForecastsActivity generateForecastsActivity,
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

        logger.LogInformation(
            "OnDemandForecastFunction: generating forecasts on demand for userId={UserId}.", request.UserId);

        User? user = await userRepository.GetByIdWithProfileAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning(
                "OnDemandForecastFunction: user {UserId} not found.", request.UserId);
            return;
        }

        if (user.SolarProfile is null)
        {
            logger.LogWarning(
                "OnDemandForecastFunction: user {UserId} has no SolarProfile.", request.UserId);
            return;
        }

        DateTime slFrom = DateTime.Now.Date;  // SL local (WEBSITE_TIME_ZONE = Sri Lanka Standard Time)
        DateTime slTo = slFrom.AddDays(13);

        Dictionary<DateTime, WeatherData> weatherMap = await generateForecastsActivity
            .LoadWeatherMapAsync(user.LocationId, slFrom, slTo, cancellationToken);

        await generateForecastsActivity.GenerateForUserAsync(user, weatherMap, slFrom, slTo, cancellationToken);

        logger.LogInformation(
            "OnDemandForecastFunction: completed for userId={UserId}.", request.UserId);
    }
}
