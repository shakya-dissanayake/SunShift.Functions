using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

public class Location : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<WeatherData> WeatherData { get; set; } = [];
}
