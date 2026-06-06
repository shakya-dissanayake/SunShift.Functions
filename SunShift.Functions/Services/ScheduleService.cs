using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

/// <summary>
/// Greedy solar-first scheduling algorithm.
///
/// For each day:
///   1. Collect solar intervals that exceed the cutoff time and have positive generation.
///   2. Sort workloads by priority ascending (1 = most important).
///   3. For each workload, find the contiguous window (of the required duration) where
///      the total available solar energy is maximised, subject to every slot having enough
///      residual capacity to cover the workload's wattage.
///   4. Allocate by reducing the residual capacity for the chosen window.
///
/// Duration is in minutes; each slot = 15 min → numSlots = Duration / 15.
/// Wattage is in watts; forecast PredictedKw is in kW → divide Wattage by 1000.
/// </summary>
public class ScheduleService : IScheduleService
{
    public IReadOnlyList<Schedule> ScheduleDay(
        IReadOnlyList<Workload> workloads,
        Forecast forecast,
        DateTime slDate,
        TimeSpan cutoff)
    {
        // Only consider slots that are in the future (cutoff) and have actual solar output.
        List<ForecastInterval> intervals = forecast.Intervals
            .Where(i => i.StartTime >= cutoff && i.PredictedKw > 0)
            .OrderBy(i => i.StartTime)
            .ToList();

        int n = intervals.Count;
        if (n == 0 || workloads.Count == 0)
            return Array.Empty<Schedule>();

        // Residual solar capacity available in each slot (kW).
        double[] residual = intervals.Select(i => i.PredictedKw).ToArray();

        var schedules = new List<Schedule>();

        foreach (Workload workload in workloads.OrderBy(w => (int)w.Priority))
        {
            // Duration is in minutes; each slot is 15 minutes.
            int numSlots = Math.Max(1, workload.Duration / 15);
            double wKw = workload.Wattage / 1000.0;

            if (n < numSlots) continue; // not enough solar hours in the day

            int bestStart = -1;
            double bestScore = double.MinValue;

            for (int s = 0; s <= n - numSlots; s++)
            {
                bool valid = true;
                double score = 0;

                for (int j = s; j < s + numSlots; j++)
                {
                    if (residual[j] < wKw) { valid = false; break; }
                    score += residual[j];
                }

                if (valid && score > bestScore)
                {
                    bestScore = score;
                    bestStart = s;
                }
            }

            if (bestStart < 0) continue; // no solar window with enough capacity for this workload

            // Commit allocation — reduce residual capacity for the chosen window.
            for (int j = bestStart; j < bestStart + numSlots; j++)
                residual[j] -= wKw;

            schedules.Add(new Schedule
            {
                WorkloadId = workload.Id,
                ScheduledDate = slDate.Date,
                StartTime = intervals[bestStart].StartTime,
                EndTime = intervals[bestStart + numSlots - 1].EndTime,
                IsManual = false,
                Status = "Scheduled"
            });
        }

        return schedules;
    }
}
