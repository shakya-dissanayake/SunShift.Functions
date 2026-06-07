using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

public class User : BaseAuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public SolarProfile? SolarProfile { get; set; }
    public List<Forecast> Forecasts { get; set; } = [];
    public List<Workload> Workloads { get; set; } = [];
}
