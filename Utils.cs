using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    public static string BytesToString(int bytes)
    {
        switch (bytes)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(bytes));
            case < 1024:
                return $"{bytes} bytes";
            case < 1024 * 1024:
                return $"{bytes / 1024}KB";
            case < 1024 * 1024 * 1024:
                return $"{bytes / (1024 * 1024)}MB";
            default:
                return $"{bytes / (1024f * 1024f * 1024f):F2}GB";
        }
    }

#if DEBUG
    internal static string GenerateMDTable<T, T2>(
        T[] rows,
        (string, Func<T, T2>) idColumn,
        params (string, Func<T, string>)[] columns
    )
    {
        var ids = new T2[rows.Length];
        var strings = new string[columns.Length, rows.Length];
        var lens = new int[columns.Length + 1];
        for (var i = 1; i <= columns.Length; i++)
        {
            var column = columns[i - 1];
            lens[i] = column.Item1.Length;

            for (var j = 0; j < rows.Length; j++)
            {
                var len = (strings[i - 1, j] = column.Item2(rows[j])).Length;
                if (len > lens[i])
                    lens[i] = len;
            }
        }

        for (var j = 0; j < rows.Length; j++)
        {
            var len = ((ids[j] = idColumn.Item2(rows[j]))?.ToString() ?? "").Length;
            if (len > lens[0])
                lens[0] = len;
        }

        var sb = new StringBuilder($"| {idColumn.Item1.PadRight(lens[0])} ");

        for (var i = 1; i <= columns.Length; i++)
        {
            sb.Append($"| {columns[i - 1].Item1.PadRight(lens[i])} ");
        }
        sb.Append("|\n");

        sb.Append("|");
        for (var j = -2; j < lens[0]; j++)
        {
            sb.Append("-");
        }
        for (var i = 1; i <= columns.Length; i++)
        {
            sb.Append("|");
            for (var j = -2; j < lens[i]; j++)
            {
                sb.Append("-");
            }
        }
        sb.Append("|\n");

        foreach (
            var kvp in rows.Select((_, k) => new KeyValuePair<int, T2>(k, ids[k]))
                .OrderBy(kvp => kvp.Value)
        )
        {
            sb.Append($"| {(ids[kvp.Key]?.ToString() ?? "").PadRight(lens[0])} ");
            for (var i = 1; i <= columns.Length; i++)
            {
                sb.Append($"| {strings[i - 1, kvp.Key].PadRight(lens[i])} ");
            }
            sb.Append("|\n");
        }

        return sb.ToString();
    }
#endif
}
