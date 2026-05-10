using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

public interface IScheduleService
{
    /// <summary>
    /// Greedy solar-optimised schedule for all workloads on a single day.
    /// Workloads are assigned in priority order (1 = highest) to the contiguous
    /// 15-minute window with the most available solar energy.
    /// A workload is only scheduled if every slot in its window has enough
    /// residual solar capacity to cover its wattage.
    /// </summary>
    /// <param name="workloads">User's active workloads.</param>
    /// <param name="forecast">Solar forecast for the day, must include Intervals.</param>
    /// <param name="slDate">Target date in SL local time.</param>
    /// <param name="cutoff">Slots with StartTime before this value are skipped (used for today).</param>
    IReadOnlyList<Schedule> ScheduleDay(
        IReadOnlyList<Workload> workloads,
        Forecast forecast,
        DateTime slDate,
        TimeSpan cutoff);
}
