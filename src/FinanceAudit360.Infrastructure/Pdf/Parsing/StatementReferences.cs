using System.Text.RegularExpressions;

namespace FinanceAudit360.Infrastructure.Pdf.Parsing;

public static partial class StatementReferences
{
    [GeneratedRegex(@"\b(?<ref>[A-Z0-9]{8,24})\b")]
    private static partial Regex Reference();

    public static string? Extract(string description)
    {
        var match = Reference().Match(description);
        return match.Success ? match.Groups["ref"].Value : null;
    }
}
