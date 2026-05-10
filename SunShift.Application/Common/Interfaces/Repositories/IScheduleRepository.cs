using SunShift.Domain.Entities;

namespace SunShift.Application.Common.Interfaces.Repositories;

public interface IScheduleRepository
{
    /// <summary>
    /// Hard-deletes all future schedule rows for the given workloads,
    /// preserving any slots that started before the cutoff on today's date.
    /// </summary>
    Task DeleteFutureAsync(
        IEnumerable<int> workloadIds,
        DateTime fromDateSl,
        TimeSpan fromTimeSl,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Schedule> schedules, CancellationToken cancellationToken = default);
}
