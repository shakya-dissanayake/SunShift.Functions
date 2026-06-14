using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class ScheduleRepository(ApplicationDbContext context) : IScheduleRepository
{
    public async Task DeleteFutureAsync(
        IEnumerable<int> workloadIds,
        DateTime fromDateSl,
        TimeSpan fromTimeSl,
        CancellationToken cancellationToken = default)
    {
        DateTime dateOnly = fromDateSl.Date;

        // Hard-delete: scheduled rows are regenerated on every run so no audit trail is needed.
        // IgnoreQueryFilters so any soft-deleted rows are also cleaned up.
        await context.Schedules
            .IgnoreQueryFilters()
            .Where(s => workloadIds.Contains(s.WorkloadId) &&
                        (s.ScheduledDate > dateOnly ||
                         (s.ScheduledDate == dateOnly && s.StartTime >= fromTimeSl)))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<List<Schedule>> GetInProgressTodayAsync(
        IEnumerable<int> workloadIds,
        DateTime dateSl,
        TimeSpan fromTimeSl,
        CancellationToken cancellationToken = default)
    {
        DateTime dateOnly = dateSl.Date;

        // The rows DeleteFutureAsync intentionally preserved: today's slots already in progress.
        return context.Schedules
            .Where(s => workloadIds.Contains(s.WorkloadId) &&
                        s.ScheduledDate == dateOnly &&
                        s.StartTime < fromTimeSl)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<Schedule> schedules,
        CancellationToken cancellationToken = default)
    {
        await context.Schedules.AddRangeAsync(schedules, cancellationToken);
    }

    // Schedules that should have their device turned ON: start time has passed, not yet started.
    public Task<List<Schedule>> GetDueToStartAsync(
        DateTime dateSl,
        TimeSpan nowTimeSl,
        CancellationToken cancellationToken = default)
        => context.Schedules
            .Include(s => s.Workload)
            .Where(s => s.ScheduledDate == dateSl.Date
                     && s.StartTime <= nowTimeSl
                     && s.EndTime > nowTimeSl
                     && s.Status == "Scheduled"
                     && s.Workload != null
                     && s.Workload.IsIoTEnabled
                     && s.Workload.IoTEntityId != null)
            .ToListAsync(cancellationToken);

    // Schedules that should have their device turned OFF: end time has passed, still marked Started.
    public Task<List<Schedule>> GetDueToEndAsync(
        DateTime dateSl,
        TimeSpan nowTimeSl,
        CancellationToken cancellationToken = default)
        => context.Schedules
            .Include(s => s.Workload)
            .Where(s => s.ScheduledDate == dateSl.Date
                     && s.EndTime <= nowTimeSl
                     && s.Status == "Started"
                     && s.Workload != null
                     && s.Workload.IsIoTEnabled
                     && s.Workload.IoTEntityId != null)
            .ToListAsync(cancellationToken);

    public Task<List<Schedule>> GetForReminderWindowAsync(
        DateTime dateSl,
        TimeSpan windowStart,
        TimeSpan windowEnd,
        CancellationToken cancellationToken = default)
        => context.Schedules
            .Include(s => s.Workload)
            .Where(s => s.ScheduledDate == dateSl.Date
                     && s.Status != "Cancelled"
                     && s.Workload != null
                     && ((s.StartTime >= windowStart && s.StartTime <= windowEnd)
                         || (s.EndTime >= windowStart && s.EndTime <= windowEnd)))
            .ToListAsync(cancellationToken);
}
