using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Infrastructure.Pdf.Parsing;

/// <summary>Result of turning a reconstructed account table into transactions.</summary>
public sealed record BankAccountTableResult(
    IReadOnlyList<ParsedTransaction> Transactions,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    int UnreconciledRows);

/// <summary>
/// Turns table rows into transactions. The deposit/withdrawal column decides the direction; when the change
/// in running balance equals the row amount its sign wins, so a mis-read column cannot flip a row.
/// </summary>
public static class BankAccountTableMapper
{
    private const decimal Tolerance = 0.01m;

    public static BankAccountTableResult Map(
        IReadOnlyList<BankAccountTableRow> rows,
        string fallbackDescription,
        Func<string, TransactionDirection, TransactionType> classify)
    {
        var transactions = new List<ParsedTransaction>();
        decimal? opening = null, previousBalance = null, closing = null;
        var unreconciled = 0;

        foreach (var row in rows)
        {
            var (amount, direction) = Amount(row);

            if (amount is null)
            {
                // B/F, or any other balance-only line: it moves the running balance but is not a transaction.
                if (row.Balance.HasValue)
                {
                    opening ??= transactions.Count == 0 ? row.Balance : null;
                    previousBalance = row.Balance;
                    closing = row.Balance;
                }

                continue;
            }

            if (previousBalance.HasValue && row.Balance.HasValue)
            {
                var delta = row.Balance.Value - previousBalance.Value;
                if (Math.Abs(Math.Abs(delta) - amount.Value) <= Tolerance)
                {
                    direction = delta >= 0 ? TransactionDirection.Credit : TransactionDirection.Debit;
                }
                else
                {
                    unreconciled++;
                }
            }

            if (row.Balance.HasValue)
            {
                previousBalance = row.Balance;
                closing = row.Balance;
            }

            transactions.Add(ToTransaction(row, amount.Value, direction, fallbackDescription, classify));
        }

        return new BankAccountTableResult(transactions, opening, closing, unreconciled);
    }

    /// <summary>Some layouts print 0.00 in the unused column, so only a non-zero figure counts.</summary>
    private static (decimal? Amount, TransactionDirection Direction) Amount(BankAccountTableRow row)
    {
        if (row.Deposit is > 0m)
        {
            return (row.Deposit, TransactionDirection.Credit);
        }

        if (row.Withdrawal is > 0m)
        {
            return (row.Withdrawal, TransactionDirection.Debit);
        }

        return (null, TransactionDirection.Debit);
    }

    private static ParsedTransaction ToTransaction(
        BankAccountTableRow row,
        decimal amount,
        TransactionDirection direction,
        string fallbackDescription,
        Func<string, TransactionDirection, TransactionType> classify)
    {
        var description = row.Particulars.Length > 0 ? row.Particulars : row.Mode ?? fallbackDescription;

        return new ParsedTransaction
        {
            TransactionDate = row.Date,
            Amount = amount,
            Direction = direction,
            TransactionType = classify($"{row.Mode} {description}", direction),
            Description = description.Truncate(1000),
            ReferenceNumber = StatementReferences.Extract(description),
            MerchantRawText = description.Truncate(500),
            Balance = row.Balance,
            RawLine = $"{row.Date:dd-MM-yyyy} | {row.Mode} | {description} | {row.Deposit} | {row.Withdrawal} | {row.Balance}".Truncate(1000)
        };
    }
}
