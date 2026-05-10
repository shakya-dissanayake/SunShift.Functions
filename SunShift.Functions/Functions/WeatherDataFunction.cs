using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace SunShift.Functions.Functions;

/// <summary>
/// Entry points that start the daily pipeline orchestrator.
/// The orchestrator chains: FetchWeatherActivity → GenerateForecastsActivity.
/// </summary>
public class DailyPipelineTrigger(ILogger<DailyPipelineTrigger> logger)
{
    /// <summary>Starts the orchestrator every day at midnight Sri Lanka time (00:00 SL = 18:30 UTC).
    /// Requires WEBSITE_TIME_ZONE = Sri Lanka Standard Time on the Function App.</summary>
    [Function("DailyPipeline_Timer")]
    public async Task TimerStartAsync(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "DailyPipeline_Timer fired at {Time:O}. IsPastDue: {IsPastDue}",
            DateTime.UtcNow, timerInfo.IsPastDue);

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DailyPipelineOrchestrator));

        logger.LogInformation("Started orchestrator. InstanceId: {Id}", instanceId);
    }

    /// <summary>Manually triggers the full pipeline via HTTP POST (Function key required).</summary>
    [Function("DailyPipeline_Manual")]
    public async Task<HttpResponseData> ManualStartAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "pipeline/trigger")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("DailyPipeline_Manual triggered at {Time:O}.", DateTime.UtcNow);

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DailyPipelineOrchestrator));

        logger.LogInformation("Started orchestrator. InstanceId: {Id}", instanceId);

        HttpResponseData response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { instanceId }, cancellationToken);
        return response;
    }
}

