using System.Globalization;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;

namespace FinanceAudit360.Infrastructure.Pdf.Pipeline.Validation;

/// <summary>
/// Final consistency checks: account statements must satisfy opening + credits - debits = closing, and
/// rows should fall inside the statement period. Failures are reported, never silently "fixed".
/// </summary>
public sealed class ReconciliationRule : IStatementValidationRule
{
    private const decimal Tolerance = 0.01m;
    private const int PeriodGraceDays = 3;

    public int Order => 100;

    public ParsedStatement Apply(ParsedStatement statement, PdfDocumentText document)
    {
        var warnings = statement.Warnings.ToList();

        if (statement.Transactions.Count == 0)
        {
            warnings.Add("Validation found no transactions. If the PDF is a scanned image it has no text to read.");
            return statement with { Warnings = warnings };
        }

        if (statement.Kind == StatementKind.BankAccount &&
            statement.OpeningBalance is { } opening &&
            statement.ClosingBalance is { } closing)
        {
            var credits = statement.Transactions.Where(t => t.Direction == TransactionDirection.Credit).Sum(t => t.Amount);
            var debits = statement.Transactions.Where(t => t.Direction == TransactionDirection.Debit).Sum(t => t.Amount);
            var expected = opening + credits - debits;

            if (Math.Abs(expected - closing) > Tolerance)
            {
                warnings.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Balances do not reconcile: opening {opening:N2} + credits {credits:N2} - debits {debits:N2} = {expected:N2}, but closing is {closing:N2}."));
            }
        }

        if (statement.PeriodStart is { } from && statement.PeriodEnd is { } to)
        {
            var outside = statement.Transactions.Count(t =>
                t.TransactionDate < from.AddDays(-PeriodGraceDays) || t.TransactionDate > to.AddDays(PeriodGraceDays));

            if (outside > 0)
            {
                warnings.Add($"{outside} transaction(s) are dated outside the statement period.");
            }
        }

        return warnings.Count == statement.Warnings.Count ? statement : statement with { Warnings = warnings };
    }
}
