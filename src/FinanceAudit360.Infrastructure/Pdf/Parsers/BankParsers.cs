using System.Text.RegularExpressions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Infrastructure.Pdf.Parsing;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Infrastructure.Pdf.Parsers;

public sealed class HdfcPdfParser : BaseStatementParser
{
    public override string Name => "HDFC";

    public override BankCode BankCode => BankCode.Hdfc;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["HDFC BANK", "HDFCBANK", "HDFC Bank Limited", "hdfcbank.com", "HDFC Bank Credit Card"];

    protected override IReadOnlyList<string> PeriodLabels =>
        ["Statement Period", "Statement for the period", "From", "Statement Date"];

    protected override IReadOnlyList<string> OpeningBalanceLabels =>
        ["Opening Balance", "Previous Balance", "Opening Bal"];
}

public sealed partial class IciciPdfParser : BaseStatementParser
{
    public override string Name => "ICICI";

    public override BankCode BankCode => BankCode.Icici;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["ICICI BANK", "ICICIBANK", "ICICI Bank Limited", "icicibank.com"];

    protected override IReadOnlyList<string> PeriodLabels =>
        ["Statement Period", "Statement Date", "Billing Period", "Period"];

    protected override IReadOnlyList<string> TotalDueLabels =>
        ["Total Amount due", "Total Amount Due", "Net Amount Due"];

    /// <summary>"... Savings Account Number: 250501507999 in INR for the period June 25, 2025 - September 29, 2025".</summary>
    [GeneratedRegex(@"for the period\s+(?<from>[A-Za-z]+\s+\d{1,2},?\s+\d{4})\s*(?:-|to)\s*(?<to>[A-Za-z]+\s+\d{1,2},?\s+\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex SavingsPeriod();

    [GeneratedRegex(@"Account\s+Number\s*[:\-]?\s*(?<number>[\dXx*]{6,20})\b", RegexOptions.IgnoreCase)]
    private static partial Regex SavingsAccountNumber();

    /// <summary>
    /// Savings statements are read from the table layout (e-statement or net-banking detailed statement):
    /// a line-based read loses every row whose particulars wrap, and cannot tell deposits from withdrawals
    /// because ICICI prints no CR/DR marker. Credit card statements keep the text path.
    /// </summary>
    protected override IReadOnlyList<BankAccountTableHeader> TableHeaders =>
        [BankAccountTableHeaders.IciciEStatement, BankAccountTableHeaders.IciciDetailed, .. BankAccountTableHeaders.Common];

    [GeneratedRegex(@"(?:for the period|Transaction Period|Period)\s*[:\-]?\s*(?:From\s*)?(?<from>\d{2}[/\-]\d{2}[/\-]\d{4})\s*(?:-|to)\s*(?<to>\d{2}[/\-]\d{2}[/\-]\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex NumericPeriod();

    public override ParsedStatement Parse(PdfDocumentText document)
    {
        var parsed = base.Parse(document);
        if (parsed.Kind != StatementKind.BankAccount)
        {
            return parsed;
        }

        var accountNumber = SavingsAccountNumber().Match(document.FullText);
        return accountNumber.Success
            ? parsed with { AccountNumberMasked = NormalizeMasked(accountNumber.Groups["number"].Value) }
            : parsed;
    }

    protected override (DateTime? From, DateTime? To) ResolvePeriod(string text, List<string> warnings)
    {
        var match = SavingsPeriod().Match(text);
        if (match.Success &&
            TryParseLongDate(match.Groups["from"].Value, out var from) &&
            TryParseLongDate(match.Groups["to"].Value, out var to))
        {
            return (from, to);
        }

        var numeric = NumericPeriod().Match(text);
        if (numeric.Success)
        {
            var start = StatementTextParser.ParseDate(numeric.Groups["from"].Value);
            var end = StatementTextParser.ParseDate(numeric.Groups["to"].Value);
            if (start is not null && end is not null)
            {
                return (start, end);
            }
        }

        return base.ResolvePeriod(text, warnings);
    }

    /// <summary>"B/F" is the brought-forward balance, not a transaction.</summary>
    protected override bool IsNoiseLine(string line) =>
        base.IsNoiseLine(line) || Regex.IsMatch(line, @"^\S+\s*B/F\b", RegexOptions.IgnoreCase);

    private static bool TryParseLongDate(string value, out DateTime date)
    {
        var ok = DateTime.TryParse(
            value.Replace(",", " ", StringComparison.Ordinal),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces,
            out var parsed);

        date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return ok;
    }
}

public sealed class CitiPdfParser : BaseStatementParser
{
    public override string Name => "Citi";

    public override BankCode BankCode => BankCode.Citi;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["CITIBANK", "CITI BANK", "CITICARDS", "CITI CARD", "citibank.co.in"];

    protected override IReadOnlyList<string> PeriodLabels =>
        ["Statement Period", "Billing Period", "Statement Date", "From Date"];
}

public sealed class SbiPdfParser : BaseStatementParser
{
    public override string Name => "SBI";

    public override BankCode BankCode => BankCode.Sbi;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["STATE BANK OF INDIA", "SBI CARD", "SBICARD", "sbicard.com", "State Bank"];

    protected override IReadOnlyList<string> PeriodLabels =>
        ["Statement Period", "Statement for the period", "Transactions for the period", "Period"];

    /// <summary>SBI Card places a trailing "D"/"C" flag instead of DR/CR on several layouts.</summary>
    protected override TransactionDirection ResolveDirection(string line, decimal? amount, StatementKind kind)
    {
        var trimmed = line.TrimEnd();

        if (trimmed.EndsWith(" C", StringComparison.Ordinal))
        {
            return TransactionDirection.Credit;
        }

        if (trimmed.EndsWith(" D", StringComparison.Ordinal))
        {
            return TransactionDirection.Debit;
        }

        return base.ResolveDirection(line, amount, kind);
    }
}

public sealed class AxisPdfParser : BaseStatementParser
{
    public override string Name => "Axis";

    public override BankCode BankCode => BankCode.Axis;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["AXIS BANK", "AXISBANK", "Axis Bank Ltd", "axisbank.com"];

    protected override IReadOnlyList<string> PeriodLabels =>
        ["Statement Period", "Statement Date", "Period", "From Date"];
}

public sealed class KotakPdfParser : BaseStatementParser
{
    public override string Name => "Kotak";

    public override BankCode BankCode => BankCode.Kotak;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["KOTAK MAHINDRA", "KOTAK BANK", "kotak.com", "Kotak Mahindra Bank Ltd"];

    protected override IReadOnlyList<string> PeriodLabels =>
        ["Statement Period", "Period", "Statement Date"];
}

public sealed class IndusIndPdfParser : BaseStatementParser
{
    public override string Name => "IndusInd";

    public override BankCode BankCode => BankCode.IndusInd;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["INDUSIND BANK", "INDUSIND", "indusind.com"];
}

public sealed class IdfcFirstPdfParser : BaseStatementParser
{
    public override string Name => "IDFCFirst";

    public override BankCode BankCode => BankCode.IdfcFirst;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["IDFC FIRST BANK", "IDFCFIRSTBANK", "IDFC FIRST CREDIT CARD", "IDFC BANK"];
}

public sealed class SouthIndianBankPdfParser : BaseStatementParser
{
    public override string Name => "SouthIndianBank";

    public override BankCode BankCode => BankCode.SouthIndian;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["ONECARD", "ONE CARD", "SOUTH INDIAN BANK CREDIT CARD", "SIB ONECARD"];

    protected override IReadOnlyList<string> CreditCardSignals =>
        [.. base.CreditCardSignals, "onecard", "one card"];
}

public sealed class UniCardPdfParser : BaseStatementParser
{
    public override string Name => "UniCard";

    public override BankCode BankCode => BankCode.UniCard;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["UNI CARD", "UNI CARDS", "UNI CREDIT CARD", "uni.cards"];

    protected override IReadOnlyList<string> CreditCardSignals =>
        [.. base.CreditCardSignals, "uni card", "uni cards"];
}

public sealed class SlicePdfParser : BaseStatementParser
{
    public override string Name => "Slice";

    public override BankCode BankCode => BankCode.Slice;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["SLICE CARD", "SLICE CREDIT CARD", "sliceit.com"];

    protected override IReadOnlyList<string> CreditCardSignals =>
        [.. base.CreditCardSignals, "slice card", "slice credit card"];
}

public sealed class PunjabNationalBankPdfParser : BaseStatementParser
{
    public override string Name => "PunjabNationalBank";

    public override BankCode BankCode => BankCode.PunjabNational;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["PUNJAB NATIONAL BANK", "PNB BANK", "PNB INDIA", "pnbindia.in"];
}

public sealed class UttarPradeshGraminBankPdfParser : BaseStatementParser
{
    public override string Name => "UttarPradeshGraminBank";

    public override BankCode BankCode => BankCode.UttarPradeshGramin;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["UTTAR PRADESH GRAMIN BANK", "UP GRAMIN BANK", "UPGB"];
}

public sealed class AirtelPaymentsBankPdfParser : BaseStatementParser
{
    public override string Name => "AirtelPaymentsBank";

    public override BankCode BankCode => BankCode.AirtelPayments;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["AIRTEL PAYMENTS BANK", "AIRTEL PAYMENT BANK", "AIRTEL BANK", "airtel.in/bank"];
}

public sealed class PaytmPostPaidPdfParser : BaseStatementParser
{
    public override string Name => "PaytmPostPaid";

    public override BankCode BankCode => BankCode.PaytmPostPaid;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["PAYTM POSTPAID", "PAYTM POST PAID", "POSTPAID BY PAYTM"];

    protected override IReadOnlyList<string> CreditCardSignals =>
        [.. base.CreditCardSignals, "paytm postpaid", "postpaid by paytm"];
}

public sealed class StandardCharteredPdfParser : BaseStatementParser
{
    public override string Name => "StandardChartered";

    public override BankCode BankCode => BankCode.StandardChartered;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["STANDARD CHARTERED", "Standard Chartered Bank", "sc.com/in"];
}

public sealed class AmexPdfParser : BaseStatementParser
{
    public override string Name => "AmericanExpress";

    public override BankCode BankCode => BankCode.AmericanExpress;

    public override int Priority => 200;

    protected override IReadOnlyList<string> Signatures =>
        ["AMERICAN EXPRESS", "AmericanExpress", "americanexpress.com", "Amex"];

    protected override IReadOnlyList<string> PeriodLabels =>
        ["Statement Period", "Closing Date", "Statement Date", "Period"];

    protected override IReadOnlyList<string> TotalDueLabels =>
        ["New Balance", "Total Amount Due", "Closing Balance"];

    /// <summary>
    /// Amex prints "May 02" without a year, so the statement period supplies the century and year.
    /// </summary>
    protected override ParsedTransaction? TryParseTransactionLine(string line, StatementKind kind, int contextYear) =>
        base.TryParseTransactionLine(line, kind, contextYear);
}

/// <summary>
/// Last-resort parser. It never claims a document during auto-detection but is used when the
/// user explicitly picks a bank the system has no dedicated parser for.
/// </summary>
public sealed class GenericPdfParser : BaseStatementParser
{
    public override string Name => "Generic";

    public override BankCode BankCode => BankCode.Other;

    public override int Priority => 0;

    protected override IReadOnlyList<string> Signatures => [];

    public override bool CanParse(PdfDocumentText document) =>
        !string.IsNullOrWhiteSpace(document.FullText) &&
        StatementTextParser.AnyAmount().IsMatch(document.FullText) &&
        StatementTextParser.AnyDate().IsMatch(document.FullText);
}
