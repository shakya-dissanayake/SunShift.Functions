using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using SunShift.Functions.Functions.Activities;

namespace SunShift.Functions.Functions;

public class DailyPipelineOrchestrator
{
    [Function(nameof(DailyPipelineOrchestrator))]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(DailyPipelineOrchestrator));
        logger.LogInformation("Daily pipeline orchestrator started. InstanceId: {Id}", context.InstanceId);

        // Step 1 – fetch fresh weather data for all locations
        await context.CallActivityAsync(nameof(FetchWeatherActivity), null);

        // Step 2 – generate solar forecasts for all users
        await context.CallActivityAsync(nameof(GenerateForecastsActivity), null);

        // Step 3 – scheduling (placeholder for future implementation)
        // await context.CallActivityAsync(nameof(GenerateSchedulesActivity), null);

        logger.LogInformation("Daily pipeline orchestrator completed.");
    }
}
