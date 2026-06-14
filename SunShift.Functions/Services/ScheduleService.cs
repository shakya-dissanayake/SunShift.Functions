using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

/// <summary>
/// Greedy solar-first scheduling algorithm (shared-budget model).
///
/// For each day:
///   1. Build a contiguous 1-minute timeline from the cutoff to end-of-day. The forecast's
///      PredictedKw (a 15-minute interval value) is applied to every minute it covers, so the
///      available solar capacity is known at minute resolution (missing minutes = 0).
///   2. Subtract solar already committed to in-progress workloads (preserved earlier today)
///      so capacity is never double-booked across a re-schedule.
///   3. Sort workloads by priority ascending (High = scheduled first).
///   4. For each workload, find the contiguous window of the required duration whose summed
///      residual solar energy is greatest. A window qualifies if it can meet the workload's
///      full energy demand from solar; if none qualifies, the sunniest window is chosen
///      best-effort (the grid tops up the shortfall).
///   5. Allocate by reducing residual capacity for the chosen window (floored at 0).
///
/// Duration is in minutes and is honoured exactly: a 10-minute load gets a 10-minute window,
/// a 90-minute load gets a 90-minute window. Wattage is in watts; forecast PredictedKw is in
/// kW → divide Wattage by 1000.
/// </summary>
public class ScheduleService : IScheduleService
{
    private const int MinutesPerDay = 1440;          // 24h × 60 min
    private const int ForecastIntervalMinutes = 15;  // forecast granularity
    private const double SlotHours = 1.0 / 60.0;     // one minute in hours

    public IReadOnlyList<Schedule> ScheduleDay(
        IReadOnlyList<Workload> workloads,
        Forecast forecast,
        DateTime slDate,
        TimeSpan cutoff,
        IReadOnlyList<Schedule>? existingSchedules = null)
    {
        if (workloads.Count == 0)
            return Array.Empty<Schedule>();

        // 1. Contiguous minute timeline: residual[minute] = available solar kW for that minute.
        // Indexing by minute-of-day keeps numSlots aligned with real elapsed time, so a
        // scheduled window is always exactly Duration long and never stretches across gaps.
        double[] residual = new double[MinutesPerDay];

        foreach (ForecastInterval interval in forecast.Intervals)
        {
            int start = MinuteIndex(interval.StartTime);
            if (start < 0 || start >= MinutesPerDay) continue;

            double kw = Math.Max(0.0, interval.PredictedKw);
            // Apply the 15-minute interval's capacity to each minute it covers.
            int end = Math.Min(start + ForecastIntervalMinutes, MinutesPerDay);
            for (int m = start; m < end; m++)
                residual[m] = kw;
        }

        // First minute still schedulable today (minutes before the cutoff are unavailable).
        // The cutoff is the exact current time — windows never start before "now", which keeps
        // the regenerate boundary aligned with the preserve/delete boundary (no duplicate rows).
        int firstSlot = Math.Clamp(MinuteIndex(cutoff), 0, MinutesPerDay);

        // 2. Subtract solar already committed to in-progress workloads preserved this run.
        if (existingSchedules is { Count: > 0 })
        {
            Dictionary<int, double> wattById = workloads.ToDictionary(w => w.Id, w => w.Wattage / 1000.0);

            foreach (Schedule existing in existingSchedules)
            {
                if (!wattById.TryGetValue(existing.WorkloadId, out double committedKw))
                    continue; // wattage unknown (workload not in this set) — can't account for it

                int start = MinuteIndex(existing.StartTime);
                int end = MinuteIndex(existing.EndTime);
                for (int s = start; s < end && s < MinutesPerDay; s++)
                {
                    if (s < 0) continue;
                    residual[s] = Math.Max(0.0, residual[s] - committedKw);
                }
            }
        }

        var schedules = new List<Schedule>();

        foreach (Workload workload in workloads.OrderBy(w => (int)w.Priority))
        {
            // Duration is in minutes; each slot is one minute, so it maps 1:1.
            int numSlots = Math.Max(1, workload.Duration);
            double wKw = workload.Wattage / 1000.0;

            // Energy the workload needs over its whole run (kWh).
            double demandKwh = wKw * numSlots * SlotHours;

            int lastStart = MinutesPerDay - numSlots;
            if (lastStart < firstSlot)
                continue; // duration longer than the remaining part of the day

            int bestStart = -1;
            double bestSolarEnergy = -1.0;     // best window that fully meets demand
            int fallbackStart = -1;
            double fallbackSolarEnergy = -1.0; // sunniest window overall (best-effort)

            // Sliding-window sum of residual solar energy.
            double windowKwh = SumEnergy(residual, firstSlot, numSlots);

            for (int s = firstSlot; s <= lastStart; s++)
            {
                if (s > firstSlot)
                {
                    windowKwh -= residual[s - 1] * SlotHours;
                    windowKwh += residual[s + numSlots - 1] * SlotHours;
                }

                if (windowKwh > fallbackSolarEnergy)
                {
                    fallbackSolarEnergy = windowKwh;
                    fallbackStart = s;
                }

                // Prefer the sunniest window that can fully cover the load from solar.
                if (windowKwh + 1e-9 >= demandKwh && windowKwh > bestSolarEnergy)
                {
                    bestSolarEnergy = windowKwh;
                    bestStart = s;
                }
            }

            // Best-effort: if nothing fully covers demand, use the sunniest window anyway.
            int chosen = bestStart >= 0 ? bestStart : fallbackStart;
            if (chosen < 0)
                continue; // no room at all on this day

            // Commit allocation — reduce residual capacity for the chosen window (floored at 0).
            for (int j = chosen; j < chosen + numSlots; j++)
                residual[j] = Math.Max(0.0, residual[j] - wKw);

            schedules.Add(new Schedule
            {
                WorkloadId = workload.Id,
                ScheduledDate = slDate.Date,
                StartTime = SlotStart(chosen),
                EndTime = SlotStart(chosen + numSlots),
                IsManual = false,
                Status = "Scheduled"
            });
        }

        return schedules;
    }

    // Helpers
    private static int MinuteIndex(TimeSpan timeOfDay) =>
        (int)timeOfDay.TotalMinutes;

    private static TimeSpan SlotStart(int minute)
    {
        // numSlots can push the end exactly to 24:00 — clamp to end-of-day.
        if (minute >= MinutesPerDay)
            return TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1);
        return TimeSpan.FromMinutes(minute);
    }

    private static double SumEnergy(double[] residual, int start, int count)
    {
        double sum = 0;
        for (int i = start; i < start + count && i < residual.Length; i++)
            sum += residual[i] * SlotHours;
        return sum;
    }
}
