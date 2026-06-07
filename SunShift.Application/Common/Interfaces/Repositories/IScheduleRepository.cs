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

    /// <summary>
    /// Returns the schedules for the given workloads on <paramref name="dateSl"/> that started
    /// before <paramref name="fromTimeSl"/> — i.e. the in-progress rows preserved by
    /// <see cref="DeleteFutureAsync"/>. Their solar draw must be reserved when re-scheduling today.
    /// </summary>
    Task<List<Schedule>> GetInProgressTodayAsync(
        IEnumerable<int> workloadIds,
        DateTime dateSl,
        TimeSpan fromTimeSl,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Schedule> schedules, CancellationToken cancellationToken = default);
}
