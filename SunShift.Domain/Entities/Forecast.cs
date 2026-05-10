using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

public class Forecast : BaseAuditableEntity
{
    public int UserId { get; set; }

    /// <summary>Target date in Sri Lanka local time (stored as SQL date, no time component).</summary>
    public DateTime TargetDate { get; set; }

    public double TotalPredictedKwh { get; set; }
    public User? User { get; set; }
    public List<ForecastInterval> Intervals { get; set; } = [];
}
