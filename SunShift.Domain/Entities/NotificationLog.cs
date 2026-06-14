using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

/// <summary>
/// Records a push notification that was sent for a given schedule + kind, so the
/// reminder scanner (which runs every minute) never sends the same reminder twice.
/// </summary>
public class NotificationLog : BaseEntity
{
    public int ScheduleId { get; set; }
    public int UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public DateTime SentDate { get; set; } 
}
