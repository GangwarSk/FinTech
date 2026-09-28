using System.Globalization;

namespace FinanceAudit360.Application.Features.Statements;

/// <summary>
/// Builds the human-readable identity of an uploaded file. Every dropdown, grid and dialog goes through
/// here so the same file always reads the same way, and so the label alone is enough to tell two uploads
/// of the same-named PDF apart.
/// </summary>
public static class StatementFileLabel
{
    private const string DateFormat = "dd MMM yyyy";

    /// <summary>
    /// "ICICI Bank - statement.pdf (01 Aug 2025 - 31 Aug 2025)". Falls back to the upload date when the
    /// PDF never produced a statement, so failed and password-locked uploads stay identifiable too.
    /// </summary>
    public static string Build(
        string? bankName,
        string originalFileName,
        DateTime? periodStart,
        DateTime? periodEnd,
        DateTime uploadedOnUtc)
    {
        var prefix = string.IsNullOrWhiteSpace(bankName) ? string.Empty : $"{bankName} - ";

        var suffix = periodStart.HasValue && periodEnd.HasValue
            ? $"({periodStart.Value.ToString(DateFormat, CultureInfo.InvariantCulture)} - {periodEnd.Value.ToString(DateFormat, CultureInfo.InvariantCulture)})"
            : $"(uploaded {uploadedOnUtc.ToString(DateFormat, CultureInfo.InvariantCulture)})";

        return $"{prefix}{originalFileName} {suffix}";
    }
}
