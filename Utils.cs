using System;
using System.Collections.Generic;

namespace LethalShipSort;

internal static class Utils
{
    public static string ToReadableString(this TimeSpan timeSpan)
    {
        var sb = new List<string>(4);
        var abs = timeSpan.Duration();
        if (abs.Days > 0)
            sb.Add(timeSpan.Days == 1 ? "1 day" : $"{timeSpan.Days} days");
        if (abs.Hours > 0)
            sb.Add(timeSpan.Hours == 1 ? "1 hour" : $"{timeSpan.Hours} hours");
        if (abs.Minutes > 0)
            sb.Add(timeSpan.Minutes == 1 ? "1 minute" : $"{timeSpan.Minutes} minutes");
        if (abs.Seconds > 0)
            sb.Add(timeSpan.Seconds == 1 ? "1 second" : $"{timeSpan.Seconds} seconds");

        if (sb.Count > 0)
            return string.Join(", ", sb);
        return "<1 second";
    }
}
