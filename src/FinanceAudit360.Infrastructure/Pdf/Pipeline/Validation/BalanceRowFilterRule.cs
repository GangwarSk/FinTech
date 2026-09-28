using System.Text.RegularExpressions;
using FinanceAudit360.Application.Common.Interfaces;

namespace FinanceAudit360.Infrastructure.Pdf.Pipeline.Validation;

/// <summary>
/// Brought/carried-forward and opening/closing balance lines are read as rows by the text path but are not
/// transactions; importing them books the balance as a debit. A leading one supplies the opening balance.
/// </summary>
public sealed partial class BalanceRowFilterRule : IStatementValidationRule
{
    [GeneratedRegex(@"^\s*(B/F|C/F|B/FWD|C/FWD|Opening\s+Bal(ance)?|Closing\s+Bal(ance)?|Balance\s+(b/f|c/f|brought\s+forward|carried\s+forward)|Brought\s+Forward|Carried\s+Forward|Total\s*$|Totals?\s*[:\-]|Total\s+(Debits?|Credits?|Withdrawals?|Deposits?)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex BalanceRow();

    public int Order => 10;

    public ParsedStatement Apply(ParsedStatement statement, PdfDocumentText document)
    {
        var kept = new List<ParsedTransaction>(statement.Transactions.Count);
        decimal? opening = statement.OpeningBalance is > 0m ? statement.OpeningBalance : null;
        var removed = 0;

        foreach (var transaction in statement.Transactions)
        {
            if (!BalanceRow().IsMatch(transaction.Description) && transaction.Amount > 0m)
            {
                kept.Add(transaction);
                continue;
            }

            removed++;
            if (kept.Count == 0 && opening is null && transaction.Amount > 0m)
            {
                opening = transaction.Balance ?? transaction.Amount;
            }
        }

        if (removed == 0)
        {
            return statement;
        }

        return statement with
        {
            OpeningBalance = opening ?? statement.OpeningBalance,
            Transactions = kept,
            Warnings = [.. statement.Warnings, $"Validation removed {removed} balance-only row(s) (B/F, C/F, totals)."]
        };
    }
}
