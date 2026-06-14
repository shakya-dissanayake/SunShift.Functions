using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Application.Models.Notifications;
using SunShift.Domain.Common;
using SunShift.Domain.Entities;

namespace SunShift.Functions.Functions;

/// <summary>
/// Scans schedules every minute and pushes reminder notifications:
///   Manual workloads    — "Turn ON {w} in 5 min", "Turn ON {w} now", "Turn OFF {w} now".
///   Automated workloads — "{w} will start in 5 min" (with Cancel), "{w} started", "{w} completed".
/// A <see cref="NotificationLog"/> row per (schedule, kind) guarantees each is sent at most once.
/// </summary>
public class NotificationReminderFunction(
    IScheduleRepository scheduleRepository,
    INotificationLogRepository notificationLogRepository,
    INotificationSender notificationSender,
    IUnitOfWork unitOfWork,
    ILogger<NotificationReminderFunction> logger)
{
    // Lead time for the "in 5 minutes" pre-warning.
    private static readonly TimeSpan Lead = TimeSpan.FromMinutes(5);

    // Window after a start/end instant during which we still fire the "now"/"started"/"completed"
    // reminder. Wide enough to absorb the dispatcher's 2-min cadence and a late timer tick; dedup
    // prevents repeats inside the window.
    private static readonly TimeSpan ActionWindow = TimeSpan.FromMinutes(3);

    [Function(nameof(NotificationReminderFunction))]
    public async Task RunAsync(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        DateTime slNow = SriLankaTime.Now;
        DateTime slDate = slNow.Date;
        TimeSpan nowT = slNow.TimeOfDay;

        // Pull every schedule whose start or end is near "now": from one ActionWindow in the past
        // (to still catch "now"/"started"/"completed") up to Lead in the future (the pre-warning).
        TimeSpan windowStart = nowT - ActionWindow;
        TimeSpan windowEnd = nowT + Lead;

        List<Schedule> candidates = await scheduleRepository.GetForReminderWindowAsync(
            slDate, windowStart, windowEnd, cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        HashSet<(int, NotificationKind)> alreadySent = await notificationLogRepository.GetSentAsync(
            candidates.Select(s => s.Id), cancellationToken);

        int sent = 0;

        foreach (Schedule schedule in candidates)
        {
            NotificationKind? kind = Classify(schedule, nowT);
            if (kind is null)
            {
                continue;
            }

            if (alreadySent.Contains((schedule.Id, kind.Value)))
            {
                continue;
            }

            PushMessage message = BuildMessage(schedule, kind.Value);
            int userId = schedule.Workload!.UserId;

            int delivered = await notificationSender.SendToUserAsync(userId, message, cancellationToken);

            // Log regardless of delivered count: a user with zero devices should not be re-evaluated
            // every minute, and re-sending once a device registers is not worth the duplicate risk.
            await notificationLogRepository.AddAsync(
                new NotificationLog
                {
                    ScheduleId = schedule.Id,
                    UserId = userId,
                    Kind = kind.Value,
                    SentDate = slNow
                },
                cancellationToken);

            sent++;

            logger.LogInformation(
                "NotificationReminderFunction: {Kind} for schedule {ScheduleId} (user {UserId}) → {Devices} device(s).",
                kind.Value, schedule.Id, userId, delivered);
        }

        if (sent > 0)
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Decides which reminder (if any) applies to a schedule at the current time.</summary>
    private static NotificationKind? Classify(Schedule schedule, TimeSpan nowT)
    {
        bool isIoT = schedule.Workload!.IsIoTEnabled;
        TimeSpan start = schedule.StartTime;
        TimeSpan end = schedule.EndTime;

        // 1) Pre-warning: start is between now and now+Lead.
        TimeSpan toStart = start - nowT;
        if (toStart > TimeSpan.Zero && toStart <= Lead)
        {
            // Only warn about an automated run that is still pending (not already started/cancelled).
            if (isIoT)
            {
                return schedule.Status == "Scheduled" ? NotificationKind.AutoStartSoon : null;
            }
            return NotificationKind.ManualStartSoon;
        }

        // 2) Completed / "turn off now": end has just passed.
        TimeSpan sinceEnd = nowT - end;
        if (sinceEnd >= TimeSpan.Zero && sinceEnd <= ActionWindow)
        {
            if (isIoT)
            {
                // The dispatcher flips to "Completed" once it turns the device off.
                return schedule.Status == "Completed" ? NotificationKind.AutoCompleted : null;
            }
            return NotificationKind.ManualEndNow;
        }

        // 3) Started / "turn on now": start has just passed (and we're not yet at the end window).
        TimeSpan sinceStart = nowT - start;
        if (sinceStart >= TimeSpan.Zero && sinceStart <= ActionWindow)
        {
            if (isIoT)
            {
                return schedule.Status == "Started" ? NotificationKind.AutoStarted : null;
            }
            return NotificationKind.ManualStartNow;
        }

        return null;
    }

    private static PushMessage BuildMessage(Schedule schedule, NotificationKind kind)
    {
        string name = schedule.Workload!.Name;

        (string title, string body) = kind switch
        {
            NotificationKind.ManualStartSoon =>
                ("Get ready", $"Turn ON {name} in 5 minutes to use your solar power."),
            NotificationKind.ManualStartNow =>
                ("Turn it on now", $"Turn ON {name} now — your solar window has started."),
            NotificationKind.ManualEndNow =>
                ("Turn it off", $"You can turn OFF {name} now — its scheduled slot has ended."),
            NotificationKind.AutoStartSoon =>
                ("Starting soon", $"{name} will start automatically in 5 minutes."),
            NotificationKind.AutoStarted =>
                ("Started", $"{name} has started automatically."),
            NotificationKind.AutoCompleted =>
                ("Completed", $"{name} has finished its scheduled run."),
            _ => ("Reminder", name)
        };

        var data = new Dictionary<string, string>
        {
            ["type"] = "schedule_reminder",
            ["kind"] = kind.ToString(),
            ["scheduleId"] = schedule.Id.ToString(),
            ["workloadId"] = schedule.WorkloadId.ToString(),
            ["workloadName"] = name
        };

        // The 5-min pre-warning for an automated run is the only point a Cancel makes sense.
        if (kind == NotificationKind.AutoStartSoon)
        {
            data["action"] = "cancel";
        }

        return new PushMessage { Title = title, Body = body, Data = data };
    }
}
