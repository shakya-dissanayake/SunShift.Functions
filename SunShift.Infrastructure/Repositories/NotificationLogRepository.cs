using Microsoft.EntityFrameworkCore;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Domain.Entities;
using SunShift.Infrastructure.Persistence;

namespace SunShift.Infrastructure.Repositories;

public class NotificationLogRepository(ApplicationDbContext context) : INotificationLogRepository
{
    public async Task<HashSet<(int ScheduleId, NotificationKind Kind)>> GetSentAsync(
        IEnumerable<int> scheduleIds,
        CancellationToken cancellationToken = default)
    {
        List<int> ids = scheduleIds as List<int> ?? scheduleIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await context.NotificationLogs
            .AsNoTracking()
            .Where(x => ids.Contains(x.ScheduleId))
            .Select(x => new { x.ScheduleId, x.Kind })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.ScheduleId, r.Kind)).ToHashSet();
    }

    public async Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default)
        => await context.NotificationLogs.AddAsync(log, cancellationToken);
}
