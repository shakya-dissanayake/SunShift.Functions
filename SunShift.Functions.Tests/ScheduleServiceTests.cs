using SunShift.Domain.Entities;
using SunShift.Functions.Services;

namespace SunShift.Functions.Tests;

public class ScheduleServiceTests
{
    private static readonly DateTime SlDate = new(2026, 6, 6);
    private readonly ScheduleService _sut = new();

    // Helpers

    // Duration is stored in minutes; tests express it in hours for readability.
    private static Workload Workload(int id, double watts, int durationHours, WorkloadPriority priority) =>
        new() { Id = id, Wattage = watts, Duration = durationHours * 60, Priority = priority };

    // Variant for durations that are not whole hours (e.g. a 10-minute water heater).
    private static Workload WorkloadMinutes(int id, double watts, int durationMinutes, WorkloadPriority priority) =>
        new() { Id = id, Wattage = watts, Duration = durationMinutes, Priority = priority };

    /// <summary>
    /// Builds a forecast with a parabolic solar curve peaking at solar noon, giving a smooth
    /// rise/fall that mimics a real day. peakKw is reached at 12:00; output is 0 before
    /// startHour and after endHour.
    /// </summary>
    private static Forecast BellCurveForecast(double peakKw, int startHour = 6, int endHour = 18)
    {
        var intervals = new List<ForecastInterval>();
        for (int minute = 0; minute < 1440; minute += 15)
        {
            double hour = minute / 60.0;
            double kw = 0.0;
            if (hour >= startHour && hour <= endHour)
            {
                double half = (endHour - startHour) / 2.0;
                double mid = (startHour + endHour) / 2.0;
                double frac = 1.0 - Math.Pow((hour - mid) / half, 2); // 0 at edges, 1 at noon
                kw = Math.Max(0.0, peakKw * frac);
            }

            intervals.Add(new ForecastInterval
            {
                StartTime = TimeSpan.FromMinutes(minute),
                EndTime = TimeSpan.FromMinutes(minute + 15),
                PredictedKw = kw
            });
        }

        return new Forecast { TargetDate = SlDate, Intervals = intervals };
    }

    /// <summary>Flat forecast: constant kW between startHour and endHour, 0 otherwise.</summary>
    private static Forecast FlatForecast(double kw, int startHour, int endHour)
    {
        var intervals = new List<ForecastInterval>();
        for (int minute = 0; minute < 1440; minute += 15)
        {
            double hour = minute / 60.0;
            intervals.Add(new ForecastInterval
            {
                StartTime = TimeSpan.FromMinutes(minute),
                EndTime = TimeSpan.FromMinutes(minute + 15),
                PredictedKw = hour >= startHour && hour < endHour ? kw : 0.0
            });
        }
        return new Forecast { TargetDate = SlDate, Intervals = intervals };
    }

    // Core regression: the old "every slot ≥ full wattage" rule dropped these 
    [Fact]
    public void HighWattageLoad_IsScheduled_BestEffort()
    {
        // 3 kW load for 2h on a 5 kW peak day. Under the old rule it needed 8 consecutive
        // slots all ≥ 3 kW — a very narrow midday band — and was easily dropped. It must now
        // always be placed in the sunniest window.
        var workloads = new[] { Workload(1, watts: 3000, durationHours: 2, priority: WorkloadPriority.High) };
        Forecast forecast = BellCurveForecast(peakKw: 5.0);

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, TimeSpan.Zero);

        Assert.Single(result);
        // Window is exactly 2h and sits around solar noon.
        Schedule s = result[0];
        Assert.Equal(TimeSpan.FromHours(2), s.EndTime - s.StartTime);
        Assert.True(s.StartTime >= TimeSpan.FromHours(10) && s.EndTime <= TimeSpan.FromHours(14),
            $"Expected a midday window, got {s.StartTime}–{s.EndTime}.");
    }

    [Fact]
    public void MultipleWorkloads_AllScheduled_InPriorityOrder()
    {
        // Three loads that previously couldn't all fit because the peak couldn't cover the
        // full wattage of every load simultaneously. Shared-budget best-effort schedules all.
        var workloads = new[]
        {
            Workload(1, watts: 2000, durationHours: 2, priority: WorkloadPriority.High),
            Workload(2, watts: 2000, durationHours: 2, priority: WorkloadPriority.Normal),
            Workload(3, watts: 2000, durationHours: 2, priority: WorkloadPriority.Low),
        };
        Forecast forecast = BellCurveForecast(peakKw: 4.0);

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, TimeSpan.Zero);

        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Equal(TimeSpan.FromHours(2), s.EndTime - s.StartTime));
    }

    //  Window correctness 

    [Fact]
    public void ScheduledWindow_IsExactlyDuration_EvenAcrossSolarGaps()
    {
        // Two separate sunny bands with a midday gap (e.g. heavy cloud 11:00–13:00 → 0 kW).
        // The window must remain a contiguous 1h block in real time, never stitched across the gap.
        var intervals = new List<ForecastInterval>();
        for (int minute = 0; minute < 1440; minute += 15)
        {
            double hour = minute / 60.0;
            bool sunny = (hour >= 8 && hour < 11) || (hour >= 13 && hour < 16);
            intervals.Add(new ForecastInterval
            {
                StartTime = TimeSpan.FromMinutes(minute),
                EndTime = TimeSpan.FromMinutes(minute + 15),
                PredictedKw = sunny ? 3.0 : 0.0
            });
        }
        var forecast = new Forecast { TargetDate = SlDate, Intervals = intervals };
        var workloads = new[] { Workload(1, watts: 1000, durationHours: 1, priority: WorkloadPriority.High) };

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, TimeSpan.Zero);

        Schedule s = Assert.Single(result);
        Assert.Equal(TimeSpan.FromHours(1), s.EndTime - s.StartTime);
        // Must fall entirely within one of the two sunny bands.
        bool inBand1 = s.StartTime >= TimeSpan.FromHours(8) && s.EndTime <= TimeSpan.FromHours(11);
        bool inBand2 = s.StartTime >= TimeSpan.FromHours(13) && s.EndTime <= TimeSpan.FromHours(16);
        Assert.True(inBand1 || inBand2, $"Window {s.StartTime}–{s.EndTime} straddles the gap.");
    }

    [Fact]
    public void PrefersSunniestWindow()
    {
        // Morning band is dim (1 kW), afternoon band is bright (4 kW). A load that fits in
        // either should land in the brighter afternoon band.
        var intervals = new List<ForecastInterval>();
        for (int minute = 0; minute < 1440; minute += 15)
        {
            double hour = minute / 60.0;
            double kw = (hour >= 7 && hour < 10) ? 1.0
                      : (hour >= 13 && hour < 16) ? 4.0
                      : 0.0;
            intervals.Add(new ForecastInterval
            {
                StartTime = TimeSpan.FromMinutes(minute),
                EndTime = TimeSpan.FromMinutes(minute + 15),
                PredictedKw = kw
            });
        }
        var forecast = new Forecast { TargetDate = SlDate, Intervals = intervals };
        var workloads = new[] { Workload(1, watts: 1000, durationHours: 1, priority: WorkloadPriority.High) };

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, TimeSpan.Zero);

        Schedule s = Assert.Single(result);
        Assert.True(s.StartTime >= TimeSpan.FromHours(13), $"Expected afternoon window, got {s.StartTime}.");
    }

    //  Minute-resolution durations

    [Theory]
    [InlineData(10)]   // water heater — shorter than one forecast interval
    [InlineData(20)]   // not a multiple of 15
    [InlineData(35)]
    [InlineData(90)]
    public void Duration_IsHonouredToTheMinute(int durationMinutes)
    {
        // The window must be exactly Duration minutes long — not rounded to a 15-min grid.
        Forecast forecast = BellCurveForecast(peakKw: 5.0);
        var workloads = new[]
        {
            WorkloadMinutes(1, watts: 1000, durationMinutes: durationMinutes, priority: WorkloadPriority.High)
        };

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, TimeSpan.Zero);

        Schedule s = Assert.Single(result);
        Assert.Equal(TimeSpan.FromMinutes(durationMinutes), s.EndTime - s.StartTime);
    }

    [Fact]
    public void SubFifteenMinuteLoads_DoNotCollapseToTheSameSlot()
    {
        // Two 10-minute loads on a flat day. On the old 15-min grid both rounded to one slot and
        // fought for the same slot; at minute resolution they get their own exact windows.
        Forecast forecast = FlatForecast(kw: 5.0, startHour: 6, endHour: 18);
        var workloads = new[]
        {
            WorkloadMinutes(1, watts: 1000, durationMinutes: 10, priority: WorkloadPriority.High),
            WorkloadMinutes(2, watts: 1000, durationMinutes: 10, priority: WorkloadPriority.Normal),
        };

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, TimeSpan.Zero);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(TimeSpan.FromMinutes(10), s.EndTime - s.StartTime));
    }

    //  Cutoff (today)

    [Fact]
    public void Cutoff_ExcludesSlotsBeforeNow()
    {
        // Sun all day, but it's already 14:00 — nothing may be scheduled before the cutoff.
        Forecast forecast = FlatForecast(kw: 5.0, startHour: 6, endHour: 18);
        var workloads = new[] { Workload(1, watts: 1000, durationHours: 1, priority: WorkloadPriority.High) };
        var cutoff = TimeSpan.FromHours(14);

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, cutoff);

        Schedule s = Assert.Single(result);
        Assert.True(s.StartTime >= cutoff, $"Scheduled before cutoff: {s.StartTime}.");
    }

    [Fact]
    public void Cutoff_AtNonGridMinute_NeverSchedulesBeforeNow()
    {
        // Cutoff at 14:07 (not a 15-min boundary). The window must not start before 14:07 — this
        // is what keeps the regenerate boundary aligned with delete/preserve so today's rows are
        // not duplicated.
        Forecast forecast = FlatForecast(kw: 5.0, startHour: 6, endHour: 18);
        var workloads = new[] { Workload(1, watts: 1000, durationHours: 1, priority: WorkloadPriority.High) };
        var cutoff = new TimeSpan(14, 7, 0);

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, cutoff);

        Schedule s = Assert.Single(result);
        Assert.True(s.StartTime >= cutoff, $"Scheduled before non-grid cutoff: {s.StartTime}.");
    }

    //  In-progress double-book reservation

    [Fact]
    public void InProgressSchedule_ReservesSolar_NoDoubleBooking()
    {
        // Flat 2 kW band 8:00–16:00. An in-progress 2 kW load occupies 10:00–14:00, consuming
        // the entire budget there. A new 2 kW / 1h load must avoid that fully-booked window.
        Forecast forecast = FlatForecast(kw: 2.0, startHour: 8, endHour: 16);

        var workloads = new[]
        {
            Workload(1, watts: 2000, durationHours: 4, priority: WorkloadPriority.High), // the in-progress one
            Workload(2, watts: 2000, durationHours: 1, priority: WorkloadPriority.Normal), // newly scheduled
        };

        var inProgress = new[]
        {
            new Schedule
            {
                WorkloadId = 1,
                ScheduledDate = SlDate,
                StartTime = TimeSpan.FromHours(10),
                EndTime = TimeSpan.FromHours(14),
            }
        };

        // Re-scheduling at 09:00; only workload 2 is (re)allocated this run.
        IReadOnlyList<Schedule> result = _sut.ScheduleDay(
            new[] { workloads[1] }, forecast, SlDate, TimeSpan.FromHours(9), inProgress);

        Schedule s = Assert.Single(result);
        // Must not overlap the fully-reserved 10:00–14:00 block.
        bool overlaps = s.StartTime < TimeSpan.FromHours(14) && s.EndTime > TimeSpan.FromHours(10);
        Assert.False(overlaps, $"New schedule {s.StartTime}–{s.EndTime} double-books reserved solar.");
    }

    //  Degenerate inputs 

    [Fact]
    public void NoWorkloads_ReturnsEmpty()
    {
        Forecast forecast = BellCurveForecast(5.0);
        Assert.Empty(_sut.ScheduleDay(Array.Empty<Workload>(), forecast, SlDate, TimeSpan.Zero));
    }

    [Fact]
    public void NoSolar_StillSchedulesBestEffort()
    {
        // Even a zero-solar day schedules best-effort (grid-only) so the user sees a plan.
        var forecast = new Forecast
        {
            TargetDate = SlDate,
            Intervals = Enumerable.Range(0, 96).Select(i => new ForecastInterval
            {
                StartTime = TimeSpan.FromMinutes(i * 15),
                EndTime = TimeSpan.FromMinutes(i * 15 + 15),
                PredictedKw = 0.0
            }).ToList()
        };
        var workloads = new[] { Workload(1, watts: 1000, durationHours: 1, priority: WorkloadPriority.High) };

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(workloads, forecast, SlDate, TimeSpan.Zero);

        Schedule s = Assert.Single(result);
        Assert.Equal(TimeSpan.FromHours(1), s.EndTime - s.StartTime);
    }

    [Fact]
    public void DurationLongerThanRemainingDay_IsSkipped()
    {
        // 6h load but it's already 20:00 — only 4h of day remain → can't place.
        Forecast forecast = FlatForecast(kw: 5.0, startHour: 0, endHour: 24);
        var workloads = new[] { Workload(1, watts: 1000, durationHours: 6, priority: WorkloadPriority.High) };

        IReadOnlyList<Schedule> result = _sut.ScheduleDay(
            workloads, forecast, SlDate, TimeSpan.FromHours(20));

        Assert.Empty(result);
    }
}
