using System.Globalization;
using System.Text.RegularExpressions;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Infrastructure.Pdf.Parsing;

/// <summary>
/// Shared, bank-agnostic primitives for reading Indian statement text: date formats used by the
/// major issuers, Indian amount formatting and label lookups.
/// </summary>
public static partial class StatementTextParser
{
    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy", "dd/MM/yy", "dd-MM-yy",
        "d/M/yyyy", "d-M-yyyy", "d/M/yy",
        "dd MMM yyyy", "dd MMM yy", "d MMM yyyy", "d MMM yy",
        "dd-MMM-yyyy", "dd-MMM-yy", "d-MMM-yyyy", "d-MMM-yy",
        "MMM dd, yyyy", "MMM dd yyyy", "MMM d, yyyy",
        "yyyy-MM-dd", "yyyy/MM/dd"
    ];

    [GeneratedRegex(@"(?<date>\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}|\d{1,2}[\s\-][A-Za-z]{3}[\s\-,]*\d{2,4}|[A-Za-z]{3}\s+\d{1,2},?\s+\d{4})", RegexOptions.Compiled)]
    public static partial Regex AnyDate();

    [GeneratedRegex(@"(?<amount>-?(?:\d{1,3}(?:,\d{2,3})*|\d+)\.\d{2})", RegexOptions.Compiled)]
    public static partial Regex AnyAmount();

    [GeneratedRegex(@"\b(?<marker>CR|DR)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex DirectionMarker();

    [GeneratedRegex(@"[Xx*]{2,}\s*[\dXx*]{0,}\s*(?<last4>\d{4})\b", RegexOptions.Compiled)]
    public static partial Regex MaskedNumber();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled)]
    public static partial Regex Email();

    [GeneratedRegex(@"(?<!\d)(?:\+91[\s-]?)?[6-9]\d{9}(?!\d)", RegexOptions.Compiled)]
    public static partial Regex MobileNumber();

    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    public static partial Regex ExcessWhitespace();

    public static DateTime? ParseDate(string? value, int? contextYear = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim().Replace(",", " ", StringComparison.Ordinal);
        cleaned = ExcessWhitespace().Replace(cleaned, " ");

        if (DateTime.TryParseExact(cleaned, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var exact))
        {
            return NormalizeCentury(exact);
        }

        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var loose))
        {
            return NormalizeCentury(loose);
        }

        // Amex-style "May 02" or "02 May" with the year carried from the statement period.
        if (contextYear.HasValue)
        {
            foreach (var format in new[] { "dd MMM", "d MMM", "MMM dd", "MMM d" })
            {
                if (DateTime.TryParseExact(cleaned, format, CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces, out var partial))
                {
                    return new DateTime(contextYear.Value, partial.Month, partial.Day, 0, 0, 0, DateTimeKind.Utc);
                }
            }
        }

        return null;
    }

    /// <summary>Two-digit years parse into 20xx; anything beyond next year is pushed back a century.</summary>
    private static DateTime NormalizeCentury(DateTime value) =>
        DateTime.SpecifyKind(
            value.Year > DateTime.UtcNow.Year + 1 ? value.AddYears(-100) : value,
            DateTimeKind.Utc);

    public static decimal? ParseAmount(string? value) => value.ToDecimalOrNull();

    /// <summary>Returns the text that follows the first matching label on the same or next line.</summary>
    public static string? FindLabelValue(string text, params string[] labels)
    {
        foreach (var label in labels)
        {
            var pattern = $@"{Regex.Escape(label)}\s*[:\-]?\s*(?<value>.+)";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                continue;
            }

            var value = match.Groups["value"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return ExcessWhitespace().Replace(value, " ");
            }
        }

        return null;
    }

    public static decimal? FindLabelAmount(string text, params string[] labels)
    {
        var value = FindLabelValue(text, labels);
        if (value is null)
        {
            return null;
        }

        var match = AnyAmount().Match(value);
        return match.Success ? ParseAmount(match.Groups["amount"].Value) : null;
    }

    public static DateTime? FindLabelDate(string text, params string[] labels)
    {
        var value = FindLabelValue(text, labels);
        if (value is null)
        {
            return null;
        }

        var match = AnyDate().Match(value);
        return match.Success ? ParseDate(match.Groups["date"].Value) : null;
    }

    /// <summary>Finds "01/05/2024 to 31/05/2024" style ranges regardless of the separator used.</summary>
    public static (DateTime? From, DateTime? To) FindPeriod(string text, params string[] labels)
    {
        foreach (var label in labels)
        {
            var pattern = $@"{Regex.Escape(label)}\s*[:\-]?\s*(?<value>.{{0,120}})";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                continue;
            }

            var dates = AnyDate().Matches(match.Groups["value"].Value)
                .Select(m => ParseDate(m.Groups["date"].Value))
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();

            if (dates.Count >= 2)
            {
                return (dates[0], dates[1]);
            }

            if (dates.Count == 1)
            {
                return (dates[0], null);
            }
        }

        return (null, null);
    }

    public static string? FindMaskedNumber(string text)
    {
        var match = MaskedNumber().Match(text);
        return match.Success ? match.Value.Trim() : null;
    }

    public static string? FindEmail(string text)
    {
        var match = Email().Match(text);
        return match.Success ? match.Value : null;
    }

    public static string? FindMobile(string text)
    {
        var match = MobileNumber().Match(text);
        return match.Success ? match.Value.DigitsOnly() : null;
    }

    public static IEnumerable<string> Lines(string text) =>
        text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => ExcessWhitespace().Replace(line.Trim(), " "))
            .Where(line => line.Length > 0);
}
