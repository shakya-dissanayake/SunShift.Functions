namespace SunShift.Domain.Entities;

/// <summary>
/// The kind of schedule reminder pushed to a user's device. Stored on
/// <see cref="NotificationLog"/> so each (schedule, kind) is sent at most once.
/// </summary>
public enum NotificationKind
{
    /// <summary>Manual workload: "Turn ON {workload} in 5 minutes."</summary>
    ManualStartSoon = 1,

    /// <summary>Manual workload: "Turn ON {workload} now."</summary>
    ManualStartNow = 2,

    /// <summary>Manual workload: "Turn OFF {workload} now." (at end of slot)</summary>
    ManualEndNow = 3,

    /// <summary>Automated workload: "{workload} will start in 5 minutes." (with Cancel action)</summary>
    AutoStartSoon = 4,

    /// <summary>Automated workload: "{workload} has started."</summary>
    AutoStarted = 5,

    /// <summary>Automated workload: "{workload} has completed."</summary>
    AutoCompleted = 6,
}
