using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SunShift.Domain.Entities;

namespace SunShift.Functions.Services;

/// <summary>
/// Runs the LightGBM ONNX solar forecast model (opset 12) and scales output per user.
/// 
/// Input tensor: "float_input"  shape [N, 29]  float32
/// Output tensor: "variable"    shape [N, 1]   float32 in log1p(kW) space
/// 
/// Feature order (MUST match training order):
///  0  shortwave_radiation        1  direct_normal_irradiance
///  2  diffuse_radiation          3  sunshine_duration
///  4  cloud_cover                5  precipitation
///  6  temperature_2m             7  wind_speed_10m
///  8  is_day                     9  solar_elevation
/// 10  time_dec                   11 doy
/// 12  month                      13 hour_sin
/// 14  hour_cos                   15 doy_sin
/// 16  doy_cos                    17 month_sin
/// 18  month_cos                  19 shortwave_radiation_lag1
/// 20  shortwave_radiation_lag2   21 shortwave_radiation_lag4
/// 22  direct_normal_irradiance_lag1  23 direct_normal_irradiance_lag2
/// 24  cloud_cover_lag1           25 cloud_cover_lag4
/// 26  ghi_roll4                  27 ghi_roll8
/// 28  cloud_roll4
/// </summary>
public sealed class SolarForecastService : ISolarForecastService, IDisposable
{
    // Fixed training coordinates (Nawala substation)
    private const double LatDeg = 6.8895;
    private const double LonDeg = 79.8955;

    // Fleet average system size used during model training (kWp)
    private const double FleetAvgKwp = 5.0;

    // Isotropic sky model ground reflectance
    private const double Albedo = 0.20;

    private readonly InferenceSession _session;
    private readonly ILogger<SolarForecastService> _logger;

    public SolarForecastService(ILogger<SolarForecastService> logger)
    {
        _logger = logger;
        string modelPath = Path.Combine(AppContext.BaseDirectory, "solar_forecast_model.onnx");
        _session = new InferenceSession(modelPath);
    }

    public Task<List<(DateTime TargetDateSl, double TotalKwh, List<ForecastInterval> Intervals)>> GenerateAsync(
        User user,
        Dictionary<DateTime, WeatherData> weatherBySl,
        DateTime slFromDate,
        DateTime slToDate,
        CancellationToken cancellationToken = default)
    {
        SolarProfile profile = user.SolarProfile
            ?? throw new InvalidOperationException($"User {user.Id} has no SolarProfile.");

        double tiltRad = profile.TiltAngle * Math.PI / 180.0;
        double panelAzimuthDeg = ParseAzimuthDeg(profile.Azimuth);

        var results = new List<(DateTime, double, List<ForecastInterval>)>();

        for (DateTime slDate = slFromDate.Date; slDate <= slToDate.Date; slDate = slDate.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var intervalSpecs = BuildIntervalSpecs(slDate, weatherBySl);

            if (intervalSpecs.Count == 0)
            {
                _logger.LogDebug(
                    "Skipping SL date {Date:yyyy-MM-dd} for user {UserId}: no weather data.",
                    slDate, user.Id);
                continue;
            }

            // Batch inference
            int n = intervalSpecs.Count;
            float[] flatFeatures = new float[n * 29];

            for (int i = 0; i < n; i++)
            {
                intervalSpecs[i].Features.CopyTo(flatFeatures, i * 29);
            }

            float[] rawOutputs = RunInference(flatFeatures, n);

            // Post-process and scale per user
            var forecastIntervals = new List<ForecastInterval>(n);
            double totalKwh = 0.0;

            for (int i = 0; i < n; i++)
            {
                double baseKw = Math.Max(0.0, Math.Exp(rawOutputs[i]) - 1.0);
                double scaledKw = ScaleToUser(
                    baseKw,
                    profile,
                    tiltRad,
                    panelAzimuthDeg,
                    intervalSpecs[i].Weather,
                    intervalSpecs[i].SlLocalTime);

                TimeSpan start = intervalSpecs[i].SlLocalTime.TimeOfDay;
                TimeSpan end = start.Add(TimeSpan.FromMinutes(15));
                if (end.Days >= 1) end = TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1);
                forecastIntervals.Add(new ForecastInterval
                {
                    StartTime = start,
                    EndTime = end,
                    PredictedKw = scaledKw
                });

                totalKwh += scaledKw * 0.25; // 15-min interval = 0.25 h
            }

            results.Add((slDate, totalKwh, forecastIntervals));
        }

        return Task.FromResult(results);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────

    private record IntervalSpec(
        DateTime SlLocalTime,
        WeatherData Weather,
        float[] Features);

    private static List<IntervalSpec> BuildIntervalSpecs(
        DateTime slDate,
        Dictionary<DateTime, WeatherData> weatherBySl)
    {
        var specs = new List<IntervalSpec>(96);

        for (int minuteOfDay = 0; minuteOfDay < 1440; minuteOfDay += 15)
        {
            DateTime slLocalDt = slDate.AddMinutes(minuteOfDay);

            if (!weatherBySl.TryGetValue(slLocalDt, out WeatherData? w))
                continue;

            int doy = slLocalDt.DayOfYear;
            int month = slLocalDt.Month;
            double timeDec = slLocalDt.Hour + slLocalDt.Minute / 60.0;

            double solarElev = ComputeSolarElevationDeg(doy, timeDec);

            double hourSin = Math.Sin(2 * Math.PI * timeDec / 24.0);
            double hourCos = Math.Cos(2 * Math.PI * timeDec / 24.0);
            double doySin = Math.Sin(2 * Math.PI * doy / 365.0);
            double doyCos = Math.Cos(2 * Math.PI * doy / 365.0);
            double monthSin = Math.Sin(2 * Math.PI * month / 12.0);
            double monthCos = Math.Cos(2 * Math.PI * month / 12.0);

            // Lag features (look up prior SL timestamps)
            double ghiLag1 = GetGhi(weatherBySl, slLocalDt.AddMinutes(-15));
            double ghiLag2 = GetGhi(weatherBySl, slLocalDt.AddMinutes(-30));
            double ghiLag4 = GetGhi(weatherBySl, slLocalDt.AddMinutes(-60));
            double dniLag1 = GetDni(weatherBySl, slLocalDt.AddMinutes(-15));
            double dniLag2 = GetDni(weatherBySl, slLocalDt.AddMinutes(-30));
            double ccLag1  = GetCc(weatherBySl, slLocalDt.AddMinutes(-15));
            double ccLag4  = GetCc(weatherBySl, slLocalDt.AddMinutes(-60));

            // Rolling means
            double ghiRoll4  = RollingMean(weatherBySl, slLocalDt, 4, GetGhi);
            double ghiRoll8  = RollingMean(weatherBySl, slLocalDt, 8, GetGhi);
            double ccRoll4   = RollingMean(weatherBySl, slLocalDt, 4, GetCc);

            float[] features =
            [
                (float)w.ShortwaveRadiation,        //  0
                (float)w.DirectNormalIrradiance,     //  1
                (float)w.DiffuseRadiation,           //  2
                (float)w.SunshineDuration,           //  3
                (float)w.CloudCover,                 //  4
                (float)w.Precipitation,              //  5
                (float)w.Temperature,                //  6
                (float)w.WindSpeed,                  //  7
                w.IsDay ? 1f : 0f,                   //  8
                (float)solarElev,                    //  9
                (float)timeDec,                      // 10
                (float)doy,                          // 11
                (float)month,                        // 12
                (float)hourSin,                      // 13
                (float)hourCos,                      // 14
                (float)doySin,                       // 15
                (float)doyCos,                       // 16
                (float)monthSin,                     // 17
                (float)monthCos,                     // 18
                (float)ghiLag1,                      // 19
                (float)ghiLag2,                      // 20
                (float)ghiLag4,                      // 21
                (float)dniLag1,                      // 22
                (float)dniLag2,                      // 23
                (float)ccLag1,                       // 24
                (float)ccLag4,                       // 25
                (float)ghiRoll4,                     // 26
                (float)ghiRoll8,                     // 27
                (float)ccRoll4,                      // 28
            ];

            specs.Add(new IntervalSpec(slLocalDt, w, features));
        }

        return specs;
    }

    private float[] RunInference(float[] flatFeatures, int n)
    {
        var tensor = new DenseTensor<float>(flatFeatures, [n, 29]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("float_input", tensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run(inputs);
        float[] raw = outputs[0].AsEnumerable<float>().ToArray();
        return raw;
    }

    private static double ScaleToUser(
        double baseKw,
        SolarProfile profile,
        double tiltRad,
        double panelAzimuthDeg,
        WeatherData w,
        DateTime slLocalDt)
    {
        double capacityFactor = baseKw / FleetAvgKwp;

        // POA irradiance (isotropic sky model)
        int doy = slLocalDt.DayOfYear;
        double timeDec = slLocalDt.Hour + slLocalDt.Minute / 60.0;
        double elevDeg = ComputeSolarElevationDeg(doy, timeDec);
        double elevRad = elevDeg * Math.PI / 180.0;
        double azimuthDeg = ComputeSolarAzimuthDeg(doy, timeDec);

        double cosAoi = Math.Sin(elevRad) * Math.Cos(tiltRad)
                      + Math.Cos(elevRad) * Math.Sin(tiltRad)
                      * Math.Cos((azimuthDeg - panelAzimuthDeg) * Math.PI / 180.0);

        double poaDirect = w.DirectNormalIrradiance * Math.Max(0.0, cosAoi);
        double poaDiffuse = w.DiffuseRadiation * (1.0 + Math.Cos(tiltRad)) / 2.0;
        double poaReflected = w.ShortwaveRadiation * Albedo * (1.0 - Math.Cos(tiltRad)) / 2.0;
        double poaTotal = poaDirect + poaDiffuse + poaReflected;

        double poaFactor = w.ShortwaveRadiation > 1.0
            ? poaTotal / w.ShortwaveRadiation
            : 1.0;

        double predicted = capacityFactor * profile.PanelCapacityKw * poaFactor;
        return Math.Min(Math.Max(0.0, predicted), profile.InverterCapacityKw);
    }

    // ── Solar geometry (Spencer 1971) ──────────────────────────────────────

    private static double ComputeSolarElevationDeg(int doy, double timeDecSl)
    {
        double b = 2.0 * Math.PI * (doy - 1) / 365.0;

        double decl = 0.006918
                    - 0.399912 * Math.Cos(b)  + 0.070257 * Math.Sin(b)
                    - 0.006758 * Math.Cos(2 * b) + 0.000907 * Math.Sin(2 * b)
                    - 0.002697 * Math.Cos(3 * b) + 0.001480 * Math.Sin(3 * b);

        double etMin = 229.18 * (0.000075
                     + 0.001868 * Math.Cos(b)  - 0.032077 * Math.Sin(b)
                     - 0.014615 * Math.Cos(2 * b) - 0.040890 * Math.Sin(2 * b));

        // Convert SL local time to solar time using equation of time and longitude
        double utcTimeDec = timeDecSl - 5.5;
        double solarTime = utcTimeDec + etMin / 60.0 + LonDeg / 15.0;

        double hRad = (solarTime - 12.0) * 15.0 * Math.PI / 180.0;
        double latRad = LatDeg * Math.PI / 180.0;

        double sinElev = Math.Sin(latRad) * Math.Sin(decl)
                       + Math.Cos(latRad) * Math.Cos(decl) * Math.Cos(hRad);

        return Math.Asin(Math.Clamp(sinElev, -1.0, 1.0)) * 180.0 / Math.PI;
    }

    private static double ComputeSolarAzimuthDeg(int doy, double timeDecSl)
    {
        double b = 2.0 * Math.PI * (doy - 1) / 365.0;

        double decl = 0.006918
                    - 0.399912 * Math.Cos(b)  + 0.070257 * Math.Sin(b)
                    - 0.006758 * Math.Cos(2 * b) + 0.000907 * Math.Sin(2 * b)
                    - 0.002697 * Math.Cos(3 * b) + 0.001480 * Math.Sin(3 * b);

        double etMin = 229.18 * (0.000075
                     + 0.001868 * Math.Cos(b)  - 0.032077 * Math.Sin(b)
                     - 0.014615 * Math.Cos(2 * b) - 0.040890 * Math.Sin(2 * b));

        double utcTimeDec = timeDecSl - 5.5;
        double solarTime = utcTimeDec + etMin / 60.0 + LonDeg / 15.0;
        double hRad = (solarTime - 12.0) * 15.0 * Math.PI / 180.0;
        double latRad = LatDeg * Math.PI / 180.0;

        double az = Math.Atan2(
            Math.Sin(hRad),
            Math.Cos(hRad) * Math.Sin(latRad) - Math.Tan(decl) * Math.Cos(latRad));

        return (az * 180.0 / Math.PI + 180.0 + 360.0) % 360.0;
    }

    // ── Lag / rolling helpers ──────────────────────────────────────────────

    private static double GetGhi(Dictionary<DateTime, WeatherData> map, DateTime utc)
        => map.TryGetValue(utc, out var w) ? w.ShortwaveRadiation : 0.0;

    private static double GetDni(Dictionary<DateTime, WeatherData> map, DateTime utc)
        => map.TryGetValue(utc, out var w) ? w.DirectNormalIrradiance : 0.0;

    private static double GetCc(Dictionary<DateTime, WeatherData> map, DateTime utc)
        => map.TryGetValue(utc, out var w) ? w.CloudCover : 0.0;

    private static double RollingMean(
        Dictionary<DateTime, WeatherData> map,
        DateTime utcCurrent,
        int periods,
        Func<Dictionary<DateTime, WeatherData>, DateTime, double> getter)
    {
        double sum = 0;
        int count = 0;
        for (int i = 1; i <= periods; i++)
        {
            DateTime prior = utcCurrent.AddMinutes(-15 * i);
            if (map.ContainsKey(prior))
            {
                sum += getter(map, prior);
                count++;
            }
        }
        return count == 0 ? 0.0 : sum / count;
    }

    // ── Azimuth string parser ──────────────────────────────────────────────

    private static double ParseAzimuthDeg(string azimuth) =>
        azimuth.Trim().ToUpperInvariant() switch
        {
            "N"  => 0.0,
            "NE" => 45.0,
            "E"  => 90.0,
            "SE" => 135.0,
            "S"  => 180.0,
            "SW" => 225.0,
            "W"  => 270.0,
            "NW" => 315.0,
            var s when double.TryParse(s, out double deg) => deg,
            _ => 180.0  // default: south-facing (optimal for Sri Lanka)
        };

    public void Dispose() => _session.Dispose();
}
