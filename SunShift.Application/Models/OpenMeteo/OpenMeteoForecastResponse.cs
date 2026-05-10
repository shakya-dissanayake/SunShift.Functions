using System.Text.Json.Serialization;

namespace SunShift.Application.Models.OpenMeteo;

public class OpenMeteoForecastResponse
{
    [JsonPropertyName("minutely_15")]
    public MinutelyData? Minutely15 { get; set; }
}

public class MinutelyData
{
    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = [];

    [JsonPropertyName("shortwave_radiation")]
    public List<double?> ShortwaveRadiation { get; set; } = [];

    [JsonPropertyName("direct_normal_irradiance")]
    public List<double?> DirectNormalIrradiance { get; set; } = [];

    [JsonPropertyName("diffuse_radiation")]
    public List<double?> DiffuseRadiation { get; set; } = [];

    [JsonPropertyName("direct_radiation")]
    public List<double?> DirectRadiation { get; set; } = [];

    [JsonPropertyName("sunshine_duration")]
    public List<double?> SunshineDuration { get; set; } = [];

    [JsonPropertyName("cloud_cover")]
    public List<double?> CloudCover { get; set; } = [];

    [JsonPropertyName("temperature_2m")]
    public List<double?> Temperature { get; set; } = [];

    [JsonPropertyName("relative_humidity_2m")]
    public List<double?> RelativeHumidity { get; set; } = [];

    [JsonPropertyName("dew_point_2m")]
    public List<double?> DewPoint { get; set; } = [];

    [JsonPropertyName("precipitation")]
    public List<double?> Precipitation { get; set; } = [];

    [JsonPropertyName("rain")]
    public List<double?> Rain { get; set; } = [];

    [JsonPropertyName("wind_speed_10m")]
    public List<double?> WindSpeed { get; set; } = [];

    [JsonPropertyName("wind_direction_10m")]
    public List<double?> WindDirection { get; set; } = [];

    [JsonPropertyName("surface_pressure")]
    public List<double?> SurfacePressure { get; set; } = [];

    // Open-Meteo returns is_day as 0/1 integer
    [JsonPropertyName("is_day")]
    public List<int?> IsDay { get; set; } = [];
}
