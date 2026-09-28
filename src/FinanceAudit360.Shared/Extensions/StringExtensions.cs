using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceAudit360.Shared.Extensions;

public static partial class StringExtensions
{
    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiWhitespace();

    [GeneratedRegex(@"[^0-9]", RegexOptions.Compiled)]
    private static partial Regex NonDigits();

    public static string? NormalizeOrNull(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return MultiWhitespace().Replace(value.Trim(), " ");
    }

    public static string NormalizeText(this string? value) => value.NormalizeOrNull() ?? string.Empty;

    public static string DigitsOnly(this string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : NonDigits().Replace(value, string.Empty);

    /// <summary>Masks all but the last four digits, e.g. "XXXXXXXXXXXX1234".</summary>
    public static string MaskCardNumber(this string? value)
    {
        var digits = value.DigitsOnly();
        if (digits.Length <= 4)
        {
            return digits;
        }

        return new string('X', digits.Length - 4) + digits[^4..];
    }

    public static string Last4(this string? value)
    {
        var digits = value.DigitsOnly();
        return digits.Length <= 4 ? digits : digits[^4..];
    }

    public static string Truncate(this string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    public static string ToUpperInvariantSafe(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static bool ContainsIgnoreCase(this string? source, string value) =>
        source is not null && source.Contains(value, StringComparison.OrdinalIgnoreCase);

    public static decimal? ToDecimalOrNull(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("\u20B9", string.Empty, StringComparison.Ordinal)
            .Replace("Rs.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("INR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        var isTrailingNegative = cleaned.EndsWith('-');
        if (isTrailingNegative)
        {
            cleaned = cleaned[..^1].Trim();
        }

        if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return null;
        }

        return isTrailingNegative ? -parsed : parsed;
    }
}
