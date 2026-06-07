using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

/// <summary>
/// Greedy solar-first scheduling algorithm (shared-budget model).
///
/// For each day:
///   1. Build a contiguous 15-minute timeline from the cutoff to end-of-day, using the
///      forecast's PredictedKw as the available solar capacity in each slot (missing slots = 0).
///   2. Subtract solar already committed to in-progress workloads (preserved earlier today)
///      so capacity is never double-booked across a re-schedule.
///   3. Sort workloads by priority ascending (High = scheduled first).
///   4. For each workload, find the contiguous window of the required duration whose summed
///      residual solar energy is greatest. A window qualifies if it can meet the workload's
///      full energy demand from solar; if none qualifies, the sunniest window is chosen
///      best-effort (the grid tops up the shortfall).
///   5. Allocate by reducing residual capacity for the chosen window (floored at 0).
///
/// Duration is in minutes; each slot = 15 min → numSlots = Duration / 15.
/// Wattage is in watts; forecast PredictedKw is in kW → divide Wattage by 1000.
/// </summary>
public class ScheduleService : IScheduleService
{
    private const int SlotsPerDay = 96;             // 24h × 4 slots/hour
    private static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(15);
    private const double SlotHours = 0.25;          // 15 min in hours

    public IReadOnlyList<Schedule> ScheduleDay(
        IReadOnlyList<Workload> workloads,
        Forecast forecast,
        DateTime slDate,
        TimeSpan cutoff,
        IReadOnlyList<Schedule>? existingSchedules = null)
    {
        if (workloads.Count == 0)
            return Array.Empty<Schedule>();

        // 1. Contiguous timeline: residual[slot] = available solar kW for that 15-min slot.
        // Indexing by slot-of-day keeps numSlots aligned with real elapsed time, so a
        // scheduled window is always exactly Duration long and never stretches across gaps.
        double[] residual = new double[SlotsPerDay];

        foreach (ForecastInterval interval in forecast.Intervals)
        {
            int slot = SlotIndex(interval.StartTime);
            if (slot < 0 || slot >= SlotsPerDay) continue;
            residual[slot] = Math.Max(0.0, interval.PredictedKw);
        }

        // First slot still schedulable today (slots before the cutoff are unavailable).
        int firstSlot = Math.Clamp(SlotIndex(cutoff), 0, SlotsPerDay);

        // 2. Subtract solar already committed to in-progress workloads preserved this run.
        if (existingSchedules is { Count: > 0 })
        {
            Dictionary<int, double> wattById = workloads.ToDictionary(w => w.Id, w => w.Wattage / 1000.0);

            foreach (Schedule existing in existingSchedules)
            {
                if (!wattById.TryGetValue(existing.WorkloadId, out double committedKw))
                    continue; // wattage unknown (workload not in this set) — can't account for it

                int start = SlotIndex(existing.StartTime);
                int end = SlotIndex(existing.EndTime);
                for (int s = start; s < end && s < SlotsPerDay; s++)
                {
                    if (s < 0) continue;
                    residual[s] = Math.Max(0.0, residual[s] - committedKw);
                }
            }
        }

        var schedules = new List<Schedule>();

        foreach (Workload workload in workloads.OrderBy(w => (int)w.Priority))
        {
            // Duration is in minutes; each slot is 15 minutes.
            int numSlots = Math.Max(1, workload.Duration / 15);
            double wKw = workload.Wattage / 1000.0;

            // Energy the workload needs over its whole run (kWh).
            double demandKwh = wKw * numSlots * SlotHours;

            int lastStart = SlotsPerDay - numSlots;
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
    private static int SlotIndex(TimeSpan timeOfDay) =>
        (int)(timeOfDay.TotalMinutes / 15.0);

    private static TimeSpan SlotStart(int slot)
    {
        // numSlots can push the end exactly to 24:00 — clamp to end-of-day.
        if (slot >= SlotsPerDay)
            return TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1);
        return TimeSpan.FromTicks(slot * SlotLength.Ticks);
    }

    private static double SumEnergy(double[] residual, int start, int count)
    {
        double sum = 0;
        for (int i = start; i < start + count && i < residual.Length; i++)
            sum += residual[i] * SlotHours;
        return sum;
    }
}
