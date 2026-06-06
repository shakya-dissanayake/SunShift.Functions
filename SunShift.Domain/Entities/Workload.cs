using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

/// <summary>
/// Scheduling-relevant fields mirrored from the API domain.
/// The Functions solution only reads Workloads; the API manages creates/updates/deletes.
/// </summary>
public class Workload : BaseAuditableEntity
{
    public int UserId { get; set; }
    public double Wattage { get; set; }              // watts
    public int Duration { get; set; }                // minutes
    public WorkloadPriority Priority { get; set; }   // High scheduled first
    public User? User { get; set; }
}
