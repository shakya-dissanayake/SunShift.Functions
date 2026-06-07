using SunShift.Domain.Common;

namespace SunShift.Domain.Entities;

public class WeatherData : BaseAuditableEntity
{
	public string GeoKey { get; set; } = string.Empty;
	public DateTime Timestamp { get; set; }
	public double ShortwaveRadiation { get; set; }
	public double DirectRadiation { get; set; }
	public double DiffuseRadiation { get; set; }
	public double DirectNormalIrradiance { get; set; }
	public double CloudCover { get; set; }
	public double CloudCoverLow { get; set; }
	public double CloudCoverMid { get; set; }
	public double CloudCoverHigh { get; set; }
	public double Temperature { get; set; }
	public double RelativeHumidity { get; set; }
	public double DewPoint { get; set; }
	public double Precipitation { get; set; }
	public double Rain { get; set; }
	public double WindSpeed { get; set; }
	public double WindDirection { get; set; }
	public double SurfacePressure { get; set; }
	public double SunshineDuration { get; set; }
	public bool IsDay { get; set; }
}
