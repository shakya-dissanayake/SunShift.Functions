using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Functions.Services;

namespace SunShift.Functions.Functions.Activities;

public class GenerateSchedulesActivity(
    IUserRepository userRepository,
    IForecastRepository forecastRepository,
    IScheduleRepository scheduleRepository,
    IUnitOfWork unitOfWork,
    IScheduleService scheduleService,
    ILogger<GenerateSchedulesActivity> logger)
{
    [Function(nameof(GenerateSchedulesActivity))]
    public async Task RunAsync([ActivityTrigger] object? input, CancellationToken cancellationToken)
    {
        DateTime slNow = DateTime.Now;  // SL local (WEBSITE_TIME_ZONE = Sri Lanka Standard Time)
        DateTime slToday = slNow.Date;
        DateTime slTo = slToday.AddDays(13);

        List<User> users = await userRepository.GetAllActiveWithWorkloadsAsync(cancellationToken);

        if (users.Count == 0)
        {
            logger.LogInformation("GenerateSchedulesActivity: no active users found.");
            return;
        }

        IEnumerable<User> usersWithWorkloads = users.Where(u => u.Workloads.Count > 0);

        logger.LogInformation(
            "GenerateSchedulesActivity: scheduling workloads for {Count} user(s), SL range {From:yyyy-MM-dd} to {To:yyyy-MM-dd}.",
            users.Count, slToday, slTo);

        foreach (User user in usersWithWorkloads)
        {
            await GenerateForUserAsync(user, slToday, slNow.TimeOfDay, slTo, cancellationToken);
        }

        logger.LogInformation("GenerateSchedulesActivity: completed.");
    }

    // ── Called for a single user on-demand (e.g. from queue trigger) ───────

    public async Task GenerateForUserAsync(
        User user,
        DateTime slFrom,
        TimeSpan slNowTime,
        DateTime slTo,
        CancellationToken cancellationToken)
    {
        try
        {
            List<int> workloadIds = user.Workloads.Select(w => w.Id).ToList();

            // Hard-delete future schedules while preserving any past/ongoing slots for today.
            await scheduleRepository.DeleteFutureAsync(workloadIds, slFrom, slNowTime, cancellationToken);

            var allSchedules = new List<Schedule>();

            for (DateTime date = slFrom; date <= slTo; date = date.AddDays(1))
            {
                // For today, skip slots that have already started.
                TimeSpan cutoff = date == slFrom ? slNowTime : TimeSpan.Zero;

                Forecast? forecast = await forecastRepository.GetByUserAndDateAsync(
                    user.Id, date, cancellationToken);

                if (forecast is null)
                {
                    logger.LogDebug(
                        "GenerateSchedulesActivity: no forecast for user {UserId} on {Date:yyyy-MM-dd} — skipping.",
                        user.Id, date);
                    continue;
                }

                IReadOnlyList<Schedule> daySchedules =
                    scheduleService.ScheduleDay(user.Workloads, forecast, date, cutoff);

                allSchedules.AddRange(daySchedules);
            }

            await scheduleRepository.AddRangeAsync(allSchedules, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "GenerateSchedulesActivity: created {Count} schedule(s) for user {UserId}.",
                allSchedules.Count, user.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "GenerateSchedulesActivity: failed for user {UserId}.", user.Id);
        }
    }
}
