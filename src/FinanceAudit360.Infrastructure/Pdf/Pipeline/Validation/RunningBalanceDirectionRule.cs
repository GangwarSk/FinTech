using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;

namespace FinanceAudit360.Infrastructure.Pdf.Pipeline.Validation;

/// <summary>
/// For account statements the running balance is the ground truth: when the change in balance equals the
/// row amount its sign decides debit or credit, whatever the parser guessed from the text.
/// </summary>
public sealed class RunningBalanceDirectionRule : IStatementValidationRule
{
    private const decimal Tolerance = 0.01m;

    public int Order => 20;

    public ParsedStatement Apply(ParsedStatement statement, PdfDocumentText document)
    {
        if (statement.Kind != StatementKind.BankAccount || statement.Transactions.All(t => t.Balance is null))
        {
            return statement;
        }

        var previous = statement.OpeningBalance;
        var corrected = 0;
        var result = new List<ParsedTransaction>(statement.Transactions.Count);

        foreach (var transaction in statement.Transactions)
        {
            var current = transaction;
            var expected = ExpectedDirection(previous, transaction);

            if (expected is not null && expected != transaction.Direction)
            {
                current = transaction with { Direction = expected.Value, TransactionType = Retype(transaction, expected.Value) };
                corrected++;
            }

            previous = transaction.Balance ?? Advance(previous, current);
            result.Add(current);
        }

        if (corrected == 0)
        {
            return statement;
        }

        return statement with
        {
            Transactions = result,
            Warnings = [.. statement.Warnings, $"Validation corrected debit/credit on {corrected} row(s) using the running balance."]
        };
    }

    private static TransactionDirection? ExpectedDirection(decimal? previous, ParsedTransaction transaction)
    {
        if (previous is null || transaction.Balance is null)
        {
            return null;
        }

        var delta = transaction.Balance.Value - previous.Value;
        if (Math.Abs(Math.Abs(delta) - transaction.Amount) > Tolerance)
        {
            return null;
        }

        return delta >= 0 ? TransactionDirection.Credit : TransactionDirection.Debit;
    }

    private static decimal? Advance(decimal? previous, ParsedTransaction transaction) =>
        previous is null
            ? null
            : previous + (transaction.Direction == TransactionDirection.Credit ? transaction.Amount : -transaction.Amount);

    /// <summary>Only the generic debit/credit types follow the direction; specific types stay.</summary>
    private static TransactionType Retype(ParsedTransaction transaction, TransactionDirection direction) =>
        transaction.TransactionType is TransactionType.Debit or TransactionType.Credit
            ? direction == TransactionDirection.Credit ? TransactionType.Credit : TransactionType.Debit
            : transaction.TransactionType;
}
