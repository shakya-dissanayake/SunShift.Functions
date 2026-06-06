namespace SunShift.Domain.Entities;

/// <summary>
/// Scheduling priority for a workload. The numeric value is the scheduling order
/// (lower = scheduled first into the sunniest solar window), so High gets the best slot.
/// </summary>
public enum WorkloadPriority
{
    High = 1,
    Normal = 2,
    Low = 3,
}
