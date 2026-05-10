using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

public class SolarProfile : BaseAuditableEntity
{
    public int UserId { get; set; }
    public double PanelCapacityKw { get; set; }
    public double InverterCapacityKw { get; set; }
    public double TiltAngle { get; set; }
    public string Azimuth { get; set; } = string.Empty;
    public User? User { get; set; }
}
