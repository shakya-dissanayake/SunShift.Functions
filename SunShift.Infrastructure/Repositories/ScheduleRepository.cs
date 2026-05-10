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

    public async Task AddRangeAsync(
        IEnumerable<Schedule> schedules,
        CancellationToken cancellationToken = default)
    {
        await context.Schedules.AddRangeAsync(schedules, cancellationToken);
    }
}
