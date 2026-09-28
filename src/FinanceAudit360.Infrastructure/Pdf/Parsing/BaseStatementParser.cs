using System.Text.RegularExpressions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Infrastructure.Pdf.Parsing;

/// <summary>
/// Template for every bank parser. Subclasses declare the text signatures that identify their
/// issuer and may override the label sets or line handling where a statement deviates.
/// </summary>
public abstract class BaseStatementParser : IPdfParser
{
    public abstract string Name { get; }

    public abstract BankCode BankCode { get; }

    public virtual int Priority => 100;

    protected abstract IReadOnlyList<string> Signatures { get; }

    protected virtual IReadOnlyList<string> CreditCardSignals =>
        ["credit card statement", "card statement", "total amount due", "minimum amount due", "payment due date", "credit limit"];

    protected virtual IReadOnlyList<string> PeriodLabels =>
        ["statement period", "statement for the period", "period", "from date to date", "billing period", "statement cycle"];

    protected virtual IReadOnlyList<string> OpeningBalanceLabels =>
        ["opening balance", "previous balance", "previous statement balance", "balance b/f", "opening bal"];

    protected virtual IReadOnlyList<string> ClosingBalanceLabels =>
        ["closing balance", "total amount due", "new balance", "closing bal", "balance c/f"];

    protected virtual IReadOnlyList<string> MinimumDueLabels =>
        ["minimum amount due", "min amount due", "minimum due", "min. amount due"];

    protected virtual IReadOnlyList<string> TotalDueLabels =>
        ["total amount due", "total due", "total payment due", "amount due"];

    protected virtual IReadOnlyList<string> PaymentDueDateLabels =>
        ["payment due date", "due date", "pay by date"];

    protected virtual IReadOnlyList<string> StatementDateLabels =>
        ["statement date", "statement generation date", "bill date", "date of statement"];

    protected virtual IReadOnlyList<string> CreditLimitLabels =>
        ["credit limit", "total credit limit", "credit limit (including cash)"];

    protected virtual IReadOnlyList<string> AvailableLimitLabels =>
        ["available credit limit", "available limit", "available credit"];

    protected virtual IReadOnlyList<string> CardHolderLabels =>
        ["card holder", "cardholder name", "name of the cardholder", "card member name", "name"];

    protected virtual IReadOnlyList<string> CardNumberLabels =>
        ["card number", "card no", "credit card number", "card no."];

    protected virtual IReadOnlyList<string> AccountNumberLabels =>
        ["account number", "account no", "a/c no", "account no."];

    /// <summary>Credit card statements express spends as debits unless the line says otherwise.</summary>
    protected virtual bool DefaultsToDebit => true;

    public virtual bool CanParse(PdfDocumentText document)
    {
        if (document.PageCount == 0 || string.IsNullOrWhiteSpace(document.FullText))
        {
            return false;
        }

        return Signatures.Any(signature => document.FullText.ContainsIgnoreCase(signature));
    }

    public virtual ParsedStatement Parse(PdfDocumentText document) =>
        ApplyTableLayout(ParseText(document), document);

    /// <summary>
    /// Column captions of the account transaction table. When one is found in the word layout the table
    /// replaces the line-based read, which loses wrapped rows and cannot tell deposits from withdrawals
    /// on statements that print no CR/DR marker.
    /// </summary>
    protected virtual IReadOnlyList<BankAccountTableHeader> TableHeaders => BankAccountTableHeaders.Common;

    protected ParsedStatement ApplyTableLayout(ParsedStatement parsed, PdfDocumentText document)
    {
        var rows = BankAccountTableReader.Read(document.PageWords, TableHeaders);
        if (rows is null || rows.Count == 0)
        {
            return parsed;
        }

        var table = BankAccountTableMapper.Map(rows, $"{Name} transaction", ClassifyType);
        if (table.Transactions.Count == 0)
        {
            return parsed with
            {
                Warnings = [.. parsed.Warnings, $"{Name} found the transaction table but no rows with a deposit or withdrawal."]
            };
        }

        var warnings = parsed.Warnings
            .Where(w => !w.Contains("did not recognise any transaction", StringComparison.Ordinal))
            .ToList();

        if (table.UnreconciledRows > 0)
        {
            warnings.Add($"{table.UnreconciledRows} row(s) did not reconcile with the running balance; check them against the PDF.");
        }

        return parsed with
        {
            Kind = StatementKind.BankAccount,
            CardNumberMasked = null,
            AccountNumberMasked = parsed.AccountNumberMasked ?? parsed.CardNumberMasked,
            PeriodStart = parsed.PeriodStart ?? table.Transactions.MinBy(t => t.TransactionDate)?.TransactionDate,
            PeriodEnd = parsed.PeriodEnd ?? table.Transactions.MaxBy(t => t.TransactionDate)?.TransactionDate,
            StatementDate = parsed.StatementDate ?? parsed.PeriodEnd,
            OpeningBalance = table.OpeningBalance ?? parsed.OpeningBalance,
            ClosingBalance = table.ClosingBalance ?? parsed.ClosingBalance,
            MinimumDue = null,
            TotalDue = null,
            CreditLimit = null,
            AvailableCreditLimit = null,
            Transactions = table.Transactions,
            Warnings = warnings
        };
    }

    protected ParsedStatement ParseText(PdfDocumentText document)
    {
        var text = document.FullText;
        var warnings = new List<string>();

        var kind = DetectKind(text);
        var (periodStart, periodEnd) = ResolvePeriod(text, warnings);
        var contextYear = periodEnd?.Year ?? periodStart?.Year ?? DateTime.UtcNow.Year;

        var transactions = ExtractTransactions(document, kind, contextYear, warnings);

        var cardNumber = StatementTextParser.FindLabelValue(text, [.. CardNumberLabels]) ?? StatementTextParser.FindMaskedNumber(text);
        var accountNumber = StatementTextParser.FindLabelValue(text, [.. AccountNumberLabels]);

        return new ParsedStatement
        {
            BankCode = BankCode,
            Kind = kind,
            ParserName = Name,
            CardNumberMasked = kind == StatementKind.CreditCard ? NormalizeMasked(cardNumber) : null,
            AccountNumberMasked = kind == StatementKind.BankAccount ? NormalizeMasked(accountNumber ?? cardNumber) : null,
            CardHolderName = CleanName(StatementTextParser.FindLabelValue(text, [.. CardHolderLabels])),
            CustomerName = CleanName(StatementTextParser.FindLabelValue(text, "customer name", "name of the customer", "account holder")),
            CustomerEmail = StatementTextParser.FindEmail(text),
            CustomerPhone = StatementTextParser.FindMobile(text),
            AddressLine1 = StatementTextParser.FindLabelValue(text, "address"),
            PostalCode = ExtractPostalCode(text),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            StatementDate = StatementTextParser.FindLabelDate(text, [.. StatementDateLabels]),
            PaymentDueDate = StatementTextParser.FindLabelDate(text, [.. PaymentDueDateLabels]),
            OpeningBalance = StatementTextParser.FindLabelAmount(text, [.. OpeningBalanceLabels]),
            ClosingBalance = StatementTextParser.FindLabelAmount(text, [.. ClosingBalanceLabels]),
            MinimumDue = StatementTextParser.FindLabelAmount(text, [.. MinimumDueLabels]),
            TotalDue = StatementTextParser.FindLabelAmount(text, [.. TotalDueLabels]),
            CreditLimit = StatementTextParser.FindLabelAmount(text, [.. CreditLimitLabels]),
            AvailableCreditLimit = StatementTextParser.FindLabelAmount(text, [.. AvailableLimitLabels]),
            Currency = "INR",
            StatementNumber = StatementTextParser.FindLabelValue(text, "statement number", "statement no"),
            Transactions = transactions,
            Warnings = warnings
        };
    }

    protected virtual StatementKind DetectKind(string text) =>
        CreditCardSignals.Any(text.ContainsIgnoreCase) ? StatementKind.CreditCard : StatementKind.BankAccount;

    protected virtual (DateTime? From, DateTime? To) ResolvePeriod(string text, List<string> warnings)
    {
        var (from, to) = StatementTextParser.FindPeriod(text, [.. PeriodLabels]);

        if (from is not null && to is not null)
        {
            return (from, to);
        }

        var statementDate = StatementTextParser.FindLabelDate(text, [.. StatementDateLabels]);
        if (statementDate is not null)
        {
            warnings.Add("The statement period was derived from the statement date.");
            return (new DateTime(statementDate.Value.Year, statementDate.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc), statementDate);
        }

        warnings.Add("No statement period could be located in the document.");
        return (from, to);
    }

    /// <summary>
    /// Walks every line, keeps the ones that start with a date and end with an amount, and turns
    /// them into transactions. Lines matching <see cref="IsNoiseLine"/> are ignored.
    /// </summary>
    protected virtual IReadOnlyList<ParsedTransaction> ExtractTransactions(
        PdfDocumentText document,
        StatementKind kind,
        int contextYear,
        List<string> warnings)
    {
        var transactions = new List<ParsedTransaction>();

        foreach (var rawLine in StatementTextParser.Lines(document.FullText))
        {
            if (IsNoiseLine(rawLine))
            {
                continue;
            }

            var parsed = TryParseTransactionLine(rawLine, kind, contextYear);
            if (parsed is not null)
            {
                transactions.Add(parsed);
            }
        }

        if (transactions.Count == 0)
        {
            warnings.Add($"{Name} did not recognise any transaction rows in this document.");
        }

        return transactions;
    }

    protected virtual ParsedTransaction? TryParseTransactionLine(string line, StatementKind kind, int contextYear)
    {
        var dateMatches = StatementTextParser.AnyDate().Matches(line);
        if (dateMatches.Count == 0 || dateMatches[0].Index > 12)
        {
            return null;
        }

        var transactionDate = StatementTextParser.ParseDate(dateMatches[0].Groups["date"].Value, contextYear);
        if (transactionDate is null)
        {
            return null;
        }

        DateTime? postingDate = null;
        var descriptionStart = dateMatches[0].Index + dateMatches[0].Length;

        if (dateMatches.Count > 1 && dateMatches[1].Index <= descriptionStart + 3)
        {
            postingDate = StatementTextParser.ParseDate(dateMatches[1].Groups["date"].Value, contextYear);
            descriptionStart = dateMatches[1].Index + dateMatches[1].Length;
        }

        var amountMatches = StatementTextParser.AnyAmount().Matches(line);
        var trailing = amountMatches.Where(m => m.Index > descriptionStart).ToList();
        if (trailing.Count == 0)
        {
            return null;
        }

        var (amount, direction, amountIndex) = ResolveAmount(line, trailing, kind);
        if (amount is null || amount == 0m)
        {
            return null;
        }

        decimal? balance = kind == StatementKind.BankAccount && trailing.Count >= 2
            ? StatementTextParser.ParseAmount(trailing[^1].Groups["amount"].Value)
            : null;

        var description = line[descriptionStart..amountIndex].Trim(' ', '-', '|', ':');
        description = StatementTextParser.ExcessWhitespace().Replace(description, " ");
        description = StripDirectionMarker(description);

        if (description.Length < 2)
        {
            return null;
        }

        return new ParsedTransaction
        {
            TransactionDate = transactionDate.Value,
            PostingDate = postingDate,
            Amount = Math.Abs(amount.Value),
            Direction = direction,
            TransactionType = ClassifyType(description, direction),
            Description = description.Truncate(1000),
            ReferenceNumber = ExtractReference(description),
            MerchantRawText = description.Truncate(500),
            Balance = balance,
            RawLine = line.Truncate(1000)
        };
    }

    /// <summary>
    /// Picks the transaction amount out of the trailing numbers. Bank statements carry a running
    /// balance in the last column, so the value before it is used when more than one is present.
    /// </summary>
    protected virtual (decimal? Amount, TransactionDirection Direction, int AmountIndex) ResolveAmount(
        string line,
        IReadOnlyList<Match> trailingAmounts,
        StatementKind kind)
    {
        var selected = kind == StatementKind.BankAccount && trailingAmounts.Count >= 2
            ? trailingAmounts[^2]
            : trailingAmounts[^1];

        var amount = StatementTextParser.ParseAmount(selected.Groups["amount"].Value);
        var direction = ResolveDirection(line, amount, kind);

        return (amount, direction, selected.Index);
    }

    protected virtual TransactionDirection ResolveDirection(string line, decimal? amount, StatementKind kind)
    {
        var marker = StatementTextParser.DirectionMarker().Matches(line)
            .Select(m => m.Groups["marker"].Value.ToUpperInvariant())
            .LastOrDefault();

        if (marker == "CR")
        {
            return TransactionDirection.Credit;
        }

        if (marker == "DR")
        {
            return TransactionDirection.Debit;
        }

        if (amount is < 0)
        {
            return TransactionDirection.Credit;
        }

        if (LooksLikeCredit(line))
        {
            return TransactionDirection.Credit;
        }

        return DefaultsToDebit ? TransactionDirection.Debit : TransactionDirection.Credit;
    }

    protected static bool LooksLikeCredit(string line) =>
        line.ContainsIgnoreCase("payment received") ||
        line.ContainsIgnoreCase("payment - thank you") ||
        line.ContainsIgnoreCase("thank you") ||
        line.ContainsIgnoreCase("refund") ||
        line.ContainsIgnoreCase("reversal") ||
        line.ContainsIgnoreCase("cashback") ||
        line.ContainsIgnoreCase("credited") ||
        line.ContainsIgnoreCase("deposit");

    protected virtual TransactionType ClassifyType(string description, TransactionDirection direction)
    {
        if (description.ContainsIgnoreCase("emi"))
        {
            return TransactionType.Emi;
        }

        if (description.ContainsIgnoreCase("atm") || description.ContainsIgnoreCase("cash wdl") || description.ContainsIgnoreCase("cash withdrawal"))
        {
            return TransactionType.CashWithdrawal;
        }

        if (description.ContainsIgnoreCase("refund"))
        {
            return TransactionType.Refund;
        }

        if (description.ContainsIgnoreCase("reversal"))
        {
            return TransactionType.Reversal;
        }

        if (description.ContainsIgnoreCase("interest"))
        {
            return TransactionType.Interest;
        }

        if (description.ContainsIgnoreCase("fee") || description.ContainsIgnoreCase("annual charge"))
        {
            return TransactionType.Fees;
        }

        if (description.ContainsIgnoreCase("charge") || description.ContainsIgnoreCase("gst") || description.ContainsIgnoreCase("surcharge"))
        {
            return TransactionType.Charges;
        }

        if (description.ContainsIgnoreCase("cashback") || description.ContainsIgnoreCase("reward"))
        {
            return TransactionType.Reward;
        }

        if (description.ContainsIgnoreCase("payment") || description.ContainsIgnoreCase("thank you"))
        {
            return TransactionType.Payment;
        }

        if (description.ContainsIgnoreCase("neft") || description.ContainsIgnoreCase("imps") ||
            description.ContainsIgnoreCase("rtgs") || description.ContainsIgnoreCase("upi") ||
            description.ContainsIgnoreCase("transfer"))
        {
            return TransactionType.Transfer;
        }

        if (description.ContainsIgnoreCase("loan"))
        {
            return TransactionType.Loan;
        }

        return direction == TransactionDirection.Credit ? TransactionType.Credit : TransactionType.Debit;
    }

    protected virtual bool IsNoiseLine(string line)
    {
        if (line.Length < 8)
        {
            return true;
        }

        return line.ContainsIgnoreCase("page ") && line.Length < 30
               || line.ContainsIgnoreCase("statement period")
               || line.ContainsIgnoreCase("payment due date")
               || line.ContainsIgnoreCase("minimum amount due")
               || line.ContainsIgnoreCase("total amount due")
               || line.ContainsIgnoreCase("opening balance")
               || line.ContainsIgnoreCase("closing balance")
               || line.ContainsIgnoreCase("credit limit")
               || line.ContainsIgnoreCase("available credit")
               || line.ContainsIgnoreCase("reward points")
               || line.ContainsIgnoreCase("registered office")
               || line.ContainsIgnoreCase("customer care")
               || line.ContainsIgnoreCase("terms and conditions")
               || line.ContainsIgnoreCase("date transaction description")
               || line.ContainsIgnoreCase("transaction date");
    }

    protected static string? ExtractReference(string description) => StatementReferences.Extract(description);

    protected static string? ExtractPostalCode(string text)
    {
        var match = Regex.Match(text, @"(?<!\d)(?<pin>[1-9]\d{5})(?!\d)");
        return match.Success ? match.Groups["pin"].Value : null;
    }

    protected static string StripDirectionMarker(string description) =>
        Regex.Replace(description, @"\s*\b(CR|DR)\b\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();

    protected static string? NormalizeMasked(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var last4 = value.Last4();
        return last4.Length == 4 ? new string('X', 12) + last4 : null;
    }

    protected static string? CleanName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = StatementTextParser.ExcessWhitespace().Replace(value.Trim(), " ");
        var cut = cleaned.IndexOfAny([':', '|']);
        if (cut > 0)
        {
            cleaned = cleaned[..cut];
        }

        return cleaned.Length is > 1 and <= 200 ? cleaned : cleaned.Truncate(200);
    }
}
