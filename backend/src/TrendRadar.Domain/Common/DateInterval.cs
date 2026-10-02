using System.Globalization;
using System.Text.RegularExpressions;

namespace TrendRadar.Domain.Common;

public enum DatePrecision
{
    Day,
    Month,
    Quarter,
    Year,
    Approximate,
}

/// <summary>
/// Fecha incierta como intervalo [Earliest, Latest] (DATA_MODEL §1). Las fechas declaradas
/// se interpretan en hora de Colombia (UTC-5, sin horario de verano) y se guardan en UTC.
/// </summary>
public readonly partial record struct DateInterval(DateTimeOffset Earliest, DateTimeOffset Latest, DatePrecision Precision)
{
    public static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    /// <summary>
    /// Acepta "2025-03-14", "2025-03", "2025-Q1", "2025" y, con "aprox" o "~", una versión aproximada
    /// (el año declarado ampliado un año hacia cada lado). Devuelve null si no se reconoce.
    /// </summary>
    public static DateInterval? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var raw = text.Trim().ToLowerInvariant();
        var approximate = raw.StartsWith('~') || raw.Contains("aprox", StringComparison.Ordinal);
        raw = raw.TrimStart('~').Replace("aprox.", string.Empty, StringComparison.Ordinal)
            .Replace("aprox", string.Empty, StringComparison.Ordinal).Trim();

        DateInterval? parsed = raw switch
        {
            _ when DayPattern().IsMatch(raw) && DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                => Local(d, d, DatePrecision.Day),
            _ when MonthPattern().Match(raw) is { Success: true } m && Month(m) is { } first
                => Local(first, first.AddMonths(1).AddDays(-1), DatePrecision.Month),
            _ when QuarterPattern().Match(raw) is { Success: true } q
                => Quarter(int.Parse(q.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(q.Groups[2].Value, CultureInfo.InvariantCulture)),
            _ when YearPattern().IsMatch(raw)
                => Year(int.Parse(raw, CultureInfo.InvariantCulture)),
            _ => null,
        };

        if (parsed is not { } p || !approximate)
        {
            return parsed;
        }

        return new DateInterval(p.Earliest.AddYears(-1), p.Latest.AddYears(1), DatePrecision.Approximate);
    }

    public static DateInterval Exact(DateTimeOffset instant) => new(instant, instant, DatePrecision.Day);

    private static DateInterval Local(DateOnly first, DateOnly last, DatePrecision precision) => new(
        new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), ColombiaOffset).ToUniversalTime(),
        new DateTimeOffset(last.ToDateTime(TimeOnly.MaxValue), ColombiaOffset).ToUniversalTime(),
        precision);

    private static DateOnly? Month(Match m)
    {
        var year = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        return month is >= 1 and <= 12 && year >= 1900 ? new DateOnly(year, month, 1) : null;
    }

    private static DateInterval? Quarter(int year, int quarter)
    {
        if (quarter is < 1 or > 4 || year < 1900)
        {
            return null;
        }

        var first = new DateOnly(year, ((quarter - 1) * 3) + 1, 1);
        return Local(first, first.AddMonths(3).AddDays(-1), DatePrecision.Quarter);
    }

    private static DateInterval? Year(int year) =>
        year < 1900 ? null : Local(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), DatePrecision.Year);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DayPattern();

    [GeneratedRegex(@"^(\d{4})-(\d{2})$")]
    private static partial Regex MonthPattern();

    [GeneratedRegex(@"^(\d{4})-?q([1-4])$")]
    private static partial Regex QuarterPattern();

    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex YearPattern();
}
