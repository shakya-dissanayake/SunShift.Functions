using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

public interface IScheduleService
{
    /// <summary>
    /// Greedy solar-optimised schedule for all workloads on a single day (shared-budget model).
    /// Workloads are assigned in priority order (High first) to the contiguous window whose
    /// total available solar energy is greatest. A window that can fully meet the workload's
    /// energy demand from solar is preferred; otherwise the sunniest window is used best-effort
    /// (the grid tops up the shortfall), so every workload with daytime room is scheduled.
    /// Capacity consumed by earlier (higher-priority) and in-progress workloads is subtracted
    /// from the remaining solar budget so it is never double-booked.
    /// </summary>
    /// <param name="workloads">User's active workloads.</param>
    /// <param name="forecast">Solar forecast for the day, must include Intervals.</param>
    /// <param name="slDate">Target date in SL local time.</param>
    /// <param name="cutoff">Slots with StartTime before this value are skipped (used for today).</param>
    /// <param name="existingSchedules">
    /// In-progress schedules preserved earlier today whose solar draw must be reserved.
    /// Pass null/empty for future days.
    /// </param>
    IReadOnlyList<Schedule> ScheduleDay(
        IReadOnlyList<Workload> workloads,
        Forecast forecast,
        DateTime slDate,
        TimeSpan cutoff,
        IReadOnlyList<Schedule>? existingSchedules = null);
}
