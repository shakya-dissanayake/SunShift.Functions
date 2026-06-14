using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface INotificationLogRepository
{
    /// <summary>
    /// Returns the set of (ScheduleId, Kind) pairs already sent for the given schedule ids,
    /// so the scanner can skip reminders it has previously delivered.
    /// </summary>
    Task<HashSet<(int ScheduleId, NotificationKind Kind)>> GetSentAsync(
        IEnumerable<int> scheduleIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default);
}
