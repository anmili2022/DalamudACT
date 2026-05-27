using System;
using System.Globalization;

namespace DalamudACT;

internal static partial class StatsPanel
{
    private static string FormatMinimalCompactPercentText(double ratio)
    {
        var percent = Math.Clamp(ratio * 100d, 0d, 100d);
        var format = percent >= 10d || Math.Abs(percent - Math.Round(percent)) <= 0.05d
            ? "0"
            : "0.#";
        return percent.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string FormatMinimalCompactValueText(string? valueText, string fallback)
    {
        var text = FormatEmptyAsFallback(valueText, fallback).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        if (TryFormatMinimalCompactUnitValue(text, "万", out var tenThousandText))
            return tenThousandText;

        if (TryFormatMinimalCompactUnitValue(text, "亿", out var hundredMillionText))
            return hundredMillionText;

        if (TryFormatMinimalCompactUnitValue(text, "兆", out var trillionText))
            return trillionText;

        return text.Replace(",", string.Empty, StringComparison.Ordinal);
    }

    private static bool TryFormatMinimalCompactUnitValue(string text, string unit, out string compactText)
    {
        compactText = string.Empty;
        if (!text.EndsWith(unit, StringComparison.Ordinal))
            return false;

        var numericPart = text[..^unit.Length].Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        if (!double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return false;

        compactText = $"{parsed:0.##}{unit}";
        return true;
    }

    private static string JoinPair(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return string.IsNullOrWhiteSpace(right) ? "--" : right!;

        if (string.IsNullOrWhiteSpace(right))
            return left;

        return $"{left} ({right})";
    }

    private static string FormatEmptyAsZero(string? value)
        => FormatEmptyAsFallback(value, "0");

    private static string FormatEmptyAsFallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string FormatHistoryTimestamp(DateTime? utcTime)
        => utcTime.HasValue
            ? utcTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "--";

    private static double ParseMetric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0d;

        var text = value.Trim();
        if (text is "---" or "--")
            return 0d;

        text = text.Replace(",", string.Empty, StringComparison.Ordinal);
        text = text.Replace("%", string.Empty, StringComparison.Ordinal);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0d;
    }

    private static int ParseCount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var text = value.Trim();
        if (text is "---" or "--")
            return 0;

        text = text.Replace(",", string.Empty, StringComparison.Ordinal);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static long ParseLocalizedAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0L;

        var text = value.Trim();
        if (text is "---" or "--")
            return 0L;

        long multiplier = 1L;
        if (text.EndsWith("兆", StringComparison.Ordinal))
        {
            multiplier = 1_000_000_000_000L;
            text = text[..^1];
        }
        else if (text.EndsWith("亿", StringComparison.Ordinal))
        {
            multiplier = 100_000_000L;
            text = text[..^1];
        }
        else if (text.EndsWith("万", StringComparison.Ordinal))
        {
            multiplier = 10_000L;
            text = text[..^1];
        }

        text = text.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return 0L;

        return (long)Math.Round(parsed * multiplier, MidpointRounding.AwayFromZero);
    }

    private static string FormatCompactAmount(long value)
    {
        const long trillion = 1_000_000_000_000L;
        const long hundredMillion = 100_000_000L;
        const long tenThousand = 10_000L;

        var abs = Math.Abs(value);
        if (abs >= trillion)
            return FormatChineseUnit(value, trillion, "兆");
        if (abs >= hundredMillion)
            return FormatChineseUnit(value, hundredMillion, "亿");
        if (abs >= tenThousand)
            return FormatChineseUnit(value, tenThousand, "万");

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatChineseUnit(long value, long unitBase, string unit)
        => (value / (double)unitBase).ToString("0.00", CultureInfo.InvariantCulture) + unit;

    private static string FormatMetricValue(double value)
        => value.ToString("0", CultureInfo.InvariantCulture);

    private static string FallbackText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
