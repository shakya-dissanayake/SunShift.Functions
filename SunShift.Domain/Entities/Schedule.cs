using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

public class Schedule : BaseAuditableEntity
{
    public int WorkloadId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsManual { get; set; }
    public string Status { get; set; } = "Scheduled";
    public Workload? Workload { get; set; }
}
