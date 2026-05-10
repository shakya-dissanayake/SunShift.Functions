using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

public class ForecastInterval : BaseAuditableEntity
{
    public int ForecastId { get; set; }

    /// <summary>Start of the 15-minute interval in Sri Lanka local time of day.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>End of the 15-minute interval in Sri Lanka local time of day.</summary>
    public TimeSpan EndTime { get; set; }

    public double PredictedKw { get; set; }
    public Forecast? Forecast { get; set; }
}
