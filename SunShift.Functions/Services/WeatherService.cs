using Microsoft.Extensions.Logging;
using SunShift.Domain.Entities;
using SunShift.Functions.Models.OpenMeteo;
using System.Net.Http.Json;

namespace SunShift.Functions.Services;

public class WeatherService(HttpClient httpClient, ILogger<WeatherService> logger) : IWeatherService
{
    private const string BaseUrl = "https://api.open-meteo.com/v1/forecast";

    // Variables available at minutely_15 resolution on Open-Meteo Forecast API
    private static readonly string MinutelyVariables = string.Join(",",
    [
        "shortwave_radiation", "direct_normal_irradiance", "diffuse_radiation", "direct_radiation",
        "sunshine_duration", "cloud_cover", "temperature_2m", "relative_humidity_2m",
        "dew_point_2m", "precipitation", "rain", "wind_speed_10m", "wind_direction_10m",
        "surface_pressure", "is_day"
    ]);

    public async Task<IReadOnlyList<WeatherData>> FetchForecastAsync(
        int locationId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        string url = $"{BaseUrl}?latitude={latitude}&longitude={longitude}" +
                     $"&minutely_15={MinutelyVariables}&forecast_days=16&timezone=UTC";

        OpenMeteoForecastResponse? response = await httpClient
            .GetFromJsonAsync<OpenMeteoForecastResponse>(url, cancellationToken);

        if (response?.Minutely15 is null || response.Minutely15.Time.Count == 0)
        {
            logger.LogWarning(
                "Empty or null response from Open-Meteo for location {LocationId} ({Lat}, {Lon})",
                locationId, latitude, longitude);
            return [];
        }

        return MapToWeatherData(locationId, response.Minutely15);
    }

    private static List<WeatherData> MapToWeatherData(int locationId, MinutelyData data)
    {
        int count = data.Time.Count;
        List<WeatherData> result = new(count);

        for (int i = 0; i < count; i++)
        {
            result.Add(new WeatherData
            {
                LocationId = locationId,
                Timestamp = DateTime.SpecifyKind(DateTime.Parse(data.Time[i]), DateTimeKind.Utc),
                ShortwaveRadiation = data.ShortwaveRadiation.ElementAtOrDefault(i) ?? 0,
                DirectNormalIrradiance = data.DirectNormalIrradiance.ElementAtOrDefault(i) ?? 0,
                DiffuseRadiation = data.DiffuseRadiation.ElementAtOrDefault(i) ?? 0,
                DirectRadiation = data.DirectRadiation.ElementAtOrDefault(i) ?? 0,
                SunshineDuration = data.SunshineDuration.ElementAtOrDefault(i) ?? 0,
                CloudCover = data.CloudCover.ElementAtOrDefault(i) ?? 0,
                Temperature = data.Temperature.ElementAtOrDefault(i) ?? 0,
                RelativeHumidity = data.RelativeHumidity.ElementAtOrDefault(i) ?? 0,
                DewPoint = data.DewPoint.ElementAtOrDefault(i) ?? 0,
                Precipitation = data.Precipitation.ElementAtOrDefault(i) ?? 0,
                Rain = data.Rain.ElementAtOrDefault(i) ?? 0,
                WindSpeed = data.WindSpeed.ElementAtOrDefault(i) ?? 0,
                WindDirection = data.WindDirection.ElementAtOrDefault(i) ?? 0,
                SurfacePressure = data.SurfacePressure.ElementAtOrDefault(i) ?? 0,
                IsDay = (data.IsDay.ElementAtOrDefault(i) ?? 0) == 1,
                // cloud_cover_low/mid/high are not available at minutely_15 resolution
            });
        }

        return result;
    }
}
