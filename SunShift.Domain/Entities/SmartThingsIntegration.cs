using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

public class SmartThingsIntegration : BaseAuditableEntity
{
    public int UserId { get; set; }
    public SmartThingsIntegrationStatus Status { get; set; } = SmartThingsIntegrationStatus.None;
    public string? AccessTokenEncrypted { get; set; }
    public string? RefreshTokenEncrypted { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? Scope { get; set; }
    public User? User { get; set; }
}
