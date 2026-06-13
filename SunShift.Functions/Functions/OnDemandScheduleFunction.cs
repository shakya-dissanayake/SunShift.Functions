using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Application.Common.Interfaces.Services;
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
    IScheduleProgressNotifier progressNotifier,
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

        int userId = request.UserId;

        logger.LogInformation(
            "OnDemandScheduleFunction: generating schedules on demand for userId={UserId}.", userId);

        await progressNotifier.NotifyAsync(userId, "FetchingUser", 10, cancellationToken);

        User? user = await userRepository.GetByIdWithWorkloadsAsync(userId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning(
                "OnDemandScheduleFunction: user {UserId} not found.", userId);
            await progressNotifier.NotifyAsync(userId, "Failed", 0, cancellationToken);
            return;
        }

        if (user.Workloads.Count == 0)
        {
            logger.LogInformation(
                "OnDemandScheduleFunction: user {UserId} has no active workloads — nothing to schedule.",
                userId);
            // Workloads cleared is a valid terminal state: the UI should stop waiting.
            await progressNotifier.NotifyAsync(userId, "Ready", 100, cancellationToken);
            return;
        }

        DateTime slNow = DateTime.Now;  // SL local (WEBSITE_TIME_ZONE = Sri Lanka Standard Time)

        await generateSchedulesActivity.GenerateForUserAsync(
            user, slNow.Date, slNow.TimeOfDay, slNow.Date.AddDays(13), cancellationToken,
            onDayProgress: percent =>
                progressNotifier.NotifyAsync(userId, "GeneratingSchedules", percent, cancellationToken));

        await progressNotifier.NotifyAsync(userId, "Ready", 100, cancellationToken);

        logger.LogInformation(
            "OnDemandScheduleFunction: completed for userId={UserId}.", userId);
    }
}
