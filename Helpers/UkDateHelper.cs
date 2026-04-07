using System;

namespace website.Helpers;

public static class UkDateHelper
{
    private static readonly TimeZoneInfo UkTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    public static DateTime NowUk =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, UkTimeZone);

    public static DateTime TodayUk => NowUk.Date;

    public static DateTime ToUkTime(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
            return TimeZoneInfo.ConvertTimeFromUtc(dateTime, UkTimeZone);

        return dateTime;
    }
}
