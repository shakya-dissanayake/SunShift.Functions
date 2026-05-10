using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Functions.Functions.Activities;

namespace SunShift.Functions.Functions;

/// <summary>
/// Queue-triggered function that regenerates schedules for a single user on demand.
/// Fired when the API enqueues a message to "schedule-requests" after a workload
/// is created, updated (scheduling-relevant fields only), or deleted.
/// </summary>
public class OnDemandScheduleFunction(
    IUserRepository userRepository,
    GenerateSchedulesActivity generateSchedulesActivity,
    ILogger<OnDemandScheduleFunction> logger)
{
    private sealed record ScheduleRequest(int UserId);

    [Function(nameof(OnDemandScheduleFunction))]
    public async Task RunAsync(
        [QueueTrigger("schedule-requests")] string messageJson,
        CancellationToken cancellationToken)
    {
        ScheduleRequest? request = JsonSerializer.Deserialize<ScheduleRequest>(
            messageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (request is null || request.UserId <= 0)
        {
            logger.LogWarning(
                "OnDemandScheduleFunction: invalid message received: {Message}", messageJson);
            return;
        }

        logger.LogInformation(
            "OnDemandScheduleFunction: generating schedules on demand for userId={UserId}.",
            request.UserId);

        User? user = await userRepository.GetByIdWithWorkloadsAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning(
                "OnDemandScheduleFunction: user {UserId} not found.", request.UserId);
            return;
        }

        if (user.Workloads.Count == 0)
        {
            logger.LogInformation(
                "OnDemandScheduleFunction: user {UserId} has no active workloads — nothing to schedule.",
                request.UserId);
            return;
        }

        DateTime slNow = DateTime.Now;  // SL local (WEBSITE_TIME_ZONE = Sri Lanka Standard Time)

        await generateSchedulesActivity.GenerateForUserAsync(
            user, slNow.Date, slNow.TimeOfDay, slNow.Date.AddDays(13), cancellationToken);

        logger.LogInformation(
            "OnDemandScheduleFunction: completed for userId={UserId}.", request.UserId);
    }
}
