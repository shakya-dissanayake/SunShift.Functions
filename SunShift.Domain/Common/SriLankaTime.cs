namespace SunShift.Domain.Common;

/// <summary>
/// Provides the current Sri Lanka local time independent of the server's own time zone.
///
/// The app stores and reasons about all business datetimes in Sri Lanka local time
/// (UTC+5:30, no DST) with <see cref="System.DateTimeKind.Unspecified"/>. This helper
/// converts from <see cref="System.DateTime.UtcNow"/> so the result is correct whether
/// the host runs in UTC or any other zone 
/// </summary>
public static class SriLankaTime
{
    private static readonly TimeZoneInfo SriLankaTz = ResolveTimeZone();

    /// <summary>Current Sri Lanka local time. Kind is Unspecified, matching DB storage.</summary>
    public static DateTime Now =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SriLankaTz),
            DateTimeKind.Unspecified);

    /// <summary>Current Sri Lanka local date (time component zeroed).</summary>
    public static DateTime Today => Now.Date;

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
        }
    }
}
