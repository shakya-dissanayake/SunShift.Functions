using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

/// <summary>
/// An FCM registration token for one of a user's devices. The Functions solution only
/// reads these (to fan a notification out to every device); the API manages register/unregister.
/// </summary>
public class DeviceToken : BaseAuditableEntity
{
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;   // "android" | "ios"
    public DateTime LastSeenDate { get; set; }
    public User? User { get; set; }
}
