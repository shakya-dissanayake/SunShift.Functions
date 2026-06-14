using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Domain.Common;
using SunShift.Domain.Entities;

namespace SunShift.Functions.Functions;

public class WorkloadDispatcherFunction(
    IScheduleRepository scheduleRepository,
    ISmartThingsIntegrationRepository integrationRepository,
    ISmartThingsClient smartThingsClient,
    ITokenProtector tokenProtector,
    IUnitOfWork unitOfWork,
    ILogger<WorkloadDispatcherFunction> logger)
{
    // Runs every 2 minutes. Looks for schedules that should have started (turn on)
    // or ended (turn off) and sends the switch command to SmartThings.
    [Function(nameof(WorkloadDispatcherFunction))]
    public async Task RunAsync([TimerTrigger("0 */2 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        DateTime slNow = SriLankaTime.Now;
        TimeSpan slNowTime = slNow.TimeOfDay;

        List<Schedule> toStart = await scheduleRepository.GetDueToStartAsync(slNow, slNowTime, cancellationToken);
        List<Schedule> toEnd = await scheduleRepository.GetDueToEndAsync(slNow, slNowTime, cancellationToken);

        if (toStart.Count == 0 && toEnd.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "WorkloadDispatcherFunction: {On} schedule(s) to turn ON, {Off} to turn OFF.",
            toStart.Count, toEnd.Count);

        // Build a lookup of integrations by UserId so we fetch once per user.
        Dictionary<int, SmartThingsIntegration> integrationsByUser = [];

        foreach (Schedule schedule in toStart.Concat(toEnd))
        {
            int userId = schedule.Workload!.UserId;
            if (!integrationsByUser.ContainsKey(userId))
            {
                SmartThingsIntegration? integration = await GetActiveIntegrationAsync(userId, cancellationToken);
                if (integration is not null)
                {
                    integrationsByUser[userId] = integration;
                }
            }
        }

        await DispatchAsync(toStart, "on", "Started", integrationsByUser, cancellationToken);
        await DispatchAsync(toEnd, "off", "Completed", integrationsByUser, cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchAsync(
        List<Schedule> schedules,
        string command,
        string nextStatus,
        Dictionary<int, SmartThingsIntegration> integrationsByUser,
        CancellationToken cancellationToken)
    {
        foreach (Schedule schedule in schedules)
        {
            int userId = schedule.Workload!.UserId;
            string deviceId = schedule.Workload.IoTEntityId!;

            if (!integrationsByUser.TryGetValue(userId, out SmartThingsIntegration? integration))
            {
                logger.LogWarning(
                    "WorkloadDispatcherFunction: no active SmartThings integration for user {UserId} — skipping schedule {ScheduleId}.",
                    userId, schedule.Id);
                continue;
            }

            try
            {
                string accessToken = await EnsureAccessTokenAsync(integration, cancellationToken);
                await smartThingsClient.SendCommandAsync(accessToken, deviceId, command, cancellationToken);
                schedule.Status = nextStatus;

                logger.LogInformation(
                    "WorkloadDispatcherFunction: sent '{Command}' to device {DeviceId} for schedule {ScheduleId}.",
                    command, deviceId, schedule.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "WorkloadDispatcherFunction: failed to send '{Command}' for schedule {ScheduleId}.",
                    command, schedule.Id);
            }
        }
    }

    private async Task<string> EnsureAccessTokenAsync(SmartThingsIntegration integration, CancellationToken cancellationToken)
    {
        // If the token expires within 60 seconds, refresh it now before sending the command.
        if (integration.AccessTokenExpiresAtUtc.HasValue
            && integration.AccessTokenExpiresAtUtc.Value < DateTime.UtcNow.AddSeconds(60)
            && !string.IsNullOrEmpty(integration.RefreshTokenEncrypted))
        {
            string refreshToken = tokenProtector.Unprotect(integration.RefreshTokenEncrypted);
            Application.Models.SmartThings.SmartThingsTokenResponse tokens =
                await smartThingsClient.RefreshAsync(refreshToken, cancellationToken);

            integration.AccessTokenEncrypted = tokenProtector.Protect(tokens.AccessToken);
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                integration.RefreshTokenEncrypted = tokenProtector.Protect(tokens.RefreshToken);
            }
            integration.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn - 60);
        }

        if (string.IsNullOrEmpty(integration.AccessTokenEncrypted))
        {
            throw new InvalidOperationException($"Active integration for user {integration.UserId} has no access token.");
        }

        return tokenProtector.Unprotect(integration.AccessTokenEncrypted);
    }

    private Task<SmartThingsIntegration?> GetActiveIntegrationAsync(int userId, CancellationToken cancellationToken)
        => integrationRepository.GetActiveByUserIdAsync(userId, cancellationToken);
}
