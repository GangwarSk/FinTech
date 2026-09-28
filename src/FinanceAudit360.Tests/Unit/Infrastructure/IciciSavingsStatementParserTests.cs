using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Infrastructure.Pdf.Parsers;

namespace FinanceAudit360.Tests.Unit.Infrastructure;

/// <summary>
/// ICICI savings statements wrap the particulars cell over several lines while the date and amounts sit on
/// only one of them. These tests rebuild that layout word by word, the way PdfPig reports it.
/// </summary>
public class IciciSavingsStatementParserTests
{
    private const double CharWidth = 3d;
    private const double WordHeight = 6d;

    private const double DateX = 97;
    private const double ModeX = 142;
    private const double ParticularsX = 222;
    private const double DepositsRight = 475;
    private const double WithdrawalsRight = 545;
    private const double BalanceRight = 628;

    private const string HeaderText = """
        ICICI Bank
        Summary of Accounts held under Cust ID: XXXX as on September 29, 2025
        Statement of Transactions in Savings Account Number: 250501507999 in INR for the period June 25, 2025 - September 29, 2025
        """;

    [Fact]
    public void Parse_ReadsEveryWrappedRowAndSkipsBroughtForward()
    {
        var parsed = new IciciPdfParser().Parse(StatementWithRowPadding());

        Assert.Equal(StatementKind.BankAccount, parsed.Kind);
        Assert.Equal(6, parsed.Transactions.Count);
        Assert.DoesNotContain(parsed.Transactions, t => t.Description == "B/F");
        Assert.DoesNotContain(parsed.Warnings, w => w.Contains("reconcile", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_JoinsWrappedParticularsIntoOneDescription()
    {
        var parsed = new IciciPdfParser().Parse(StatementWithRowPadding());

        var sumit = parsed.Transactions.Single(t => t.Description.StartsWith("UPI/SUMIT", StringComparison.Ordinal));
        Assert.Equal(
            "UPI/SUMIT KUMA/9871994735@axl/Payment fr/FEDERAL BA/803707647614/IBL436a868263fe45d399e2893cb e50c7d3",
            sumit.Description);
        Assert.Equal(new DateTime(2025, 6, 27), sumit.TransactionDate);
        Assert.Equal(379.00m, sumit.Amount);
        Assert.Equal(TransactionDirection.Debit, sumit.Direction);
    }

    [Fact]
    public void Parse_TellsDepositsFromWithdrawalsByColumn()
    {
        var parsed = new IciciPdfParser().Parse(StatementWithRowPadding());

        var interest = parsed.Transactions.Single(t => t.Description.Contains("Int.Pd", StringComparison.Ordinal));
        Assert.Equal(TransactionDirection.Credit, interest.Direction);
        Assert.Equal(343.00m, interest.Amount);

        var cms = parsed.Transactions.Single(t => t.Description.StartsWith("CMS/", StringComparison.Ordinal));
        Assert.Equal(TransactionDirection.Credit, cms.Direction);
        Assert.Equal(175_663.00m, cms.Amount);
        Assert.Equal("CMS/ CMS152478609/R SYSTEMS INTERNATIONAL LTD", cms.Description);

        var imps = parsed.Transactions.Single(t => t.Description.StartsWith("MMT/IMPS", StringComparison.Ordinal));
        Assert.Equal(TransactionDirection.Debit, imps.Direction);
        Assert.Equal(100_000.00m, imps.Amount);
    }

    [Fact]
    public void Parse_ReadsPeriodAccountAndBalances()
    {
        var parsed = new IciciPdfParser().Parse(StatementWithRowPadding());

        Assert.Equal(new DateTime(2025, 6, 25), parsed.PeriodStart);
        Assert.Equal(new DateTime(2025, 9, 29), parsed.PeriodEnd);
        Assert.Equal(DateTimeKind.Utc, parsed.PeriodStart!.Value.Kind);
        Assert.Equal(7_881.45m, parsed.OpeningBalance);
        Assert.Equal(78_888.45m, parsed.ClosingBalance);
        Assert.EndsWith("7999", parsed.AccountNumberMasked, StringComparison.Ordinal);
        Assert.Null(parsed.CardNumberMasked);
        Assert.Null(parsed.TotalDue);
    }

    [Fact]
    public void Parse_SplitsRowsByCentringWhenThereIsNoGapBetweenThem()
    {
        var page = new PageBuilder();
        page.Header(100);

        // Uniform 6.5pt spacing everywhere, so row boundaries are only recoverable from where the dates sit.
        page.Row(113, "25-06-2025", null, ["B/F"], null, null, "1,000.00", firstLineY: 113);
        page.Row(126, "26-06-2025", null, ["UPI/ONE/line", "two", "three"], null, "100.00", "900.00", firstLineY: 119.5);
        page.Row(142.25, "27-06-2025", null, ["UPI/TWO/line", "two"], "50.00", null, "950.00", firstLineY: 139);
        page.Row(152, "28-06-2025", null, ["UPI/THREE"], null, "25.00", "925.00", firstLineY: 152);

        var parsed = new IciciPdfParser().Parse(page.Build(HeaderText));

        Assert.Equal(3, parsed.Transactions.Count);
        Assert.Equal("UPI/ONE/line two three", parsed.Transactions[0].Description);
        Assert.Equal("UPI/TWO/line two", parsed.Transactions[1].Description);
        Assert.Equal(TransactionDirection.Credit, parsed.Transactions[1].Direction);
        Assert.Equal("UPI/THREE", parsed.Transactions[2].Description);
    }

    [Fact]
    public void Parse_FallsBackToTextWhenNoWordPositionsAreAvailable()
    {
        const string text = HeaderText + "\n30-06-2025 250501507999:Int.Pd:29-03-2025 to 29-06-2025 343.00 7,701.45\n25-06-2025 B/F 7,881.45";
        var document = new PdfDocumentText([text], text, 1, false, new Dictionary<string, string>());

        var parsed = new IciciPdfParser().Parse(document);

        Assert.Single(parsed.Transactions);
        Assert.Equal(new DateTime(2025, 9, 29), parsed.PeriodEnd);
    }

    [Fact]
    public void Parse_KeepsTheTextPathForCreditCardStatements()
    {
        const string text = """
            ICICI BANK Credit Card Statement
            Statement Period: 01/06/2024 - 30/06/2024
            03/06/2024 UBER INDIA SYSTEMS 420.00
            """;
        var document = new PdfDocumentText([text], text, 1, false, new Dictionary<string, string>(), [[]]);

        var parsed = new IciciPdfParser().Parse(document);

        Assert.Equal(StatementKind.CreditCard, parsed.Kind);
        Assert.Single(parsed.Transactions);
    }

    /// <summary>The rows from the uploaded statement, with the extra space ICICI leaves between rows.</summary>
    private static PdfDocumentText StatementWithRowPadding()
    {
        var page = new PageBuilder();
        page.Header(388);

        page.Row(399, "25-06-2025", null, ["B/F"], null, null, "7,881.45", firstLineY: 399);
        page.Row(423, "27-06-2025", null,
            ["UPI/SUMIT KUMA/9871994735@axl/Payment", "fr/FEDERAL", "BA/803707647614/IBL436a868263fe45d399e2893cb", "e50c7d3"],
            null, "379.00", "7,502.45", firstLineY: 410);
        page.Row(459, "29-06-2025", null,
            ["UPI/ZEPTOONLINE@ybl/Payment from Ph/YES", "BANK", "LIMITE/662661953949/IBLd54113d96e924b48a189d", "1171973e35c"],
            null, "144.00", "7,358.45", firstLineY: 446);
        page.Row(480, "30-06-2025", null, ["250501507999:Int.Pd:29-03-2025 to 29-06-2025"], "343.00", null, "7,701.45", firstLineY: 480);
        page.Row(494, "30-06-2025", "CMS TRANSACTION",
            ["CMS/ CMS152478609/R SYSTEMS", "INTERNATIONAL LTD"],
            "1,75,663.00", null, "1,83,364.45", firstLineY: 490.75);
        page.Row(518, "30-06-2025", null,
            ["UPI/cred.club@axisb/payment on CRED/AXIS", "BANK/554702769485/ACDa378c333d2a84ac782985", "a0447d2b06c/"],
            null, "4,476.00", "1,78,888.45", firstLineY: 511.5);
        page.Row(538, "05-07-2025", "MOBILE BANKING",
            ["MMT/IMPS/518613151398/Santosh", "Ku/UTIB0004257"],
            null, "1,00,000.00", "78,888.45", firstLineY: 535);

        page.Line(560, DateX, "TOTAL");
        page.Line(560, DepositsRight - 33, "1,76,006.00");

        return page.Build(HeaderText);
    }

    private sealed class PageBuilder
    {
        private readonly List<PdfWord> _words = [];

        public void Header(double y)
        {
            Line(y, DateX, "DATE");
            Line(y, ModeX, "MODE**");
            Line(y, ParticularsX, "PARTICULARS");
            Right(y, DepositsRight, "DEPOSITS");
            Right(y, WithdrawalsRight, "WITHDRAWALS");
            Right(y, BalanceRight, "BALANCE");
        }

        public void Row(
            double anchorY,
            string date,
            string? mode,
            IReadOnlyList<string> particulars,
            string? deposit,
            string? withdrawal,
            string balance,
            double firstLineY)
        {
            Line(anchorY, DateX, date);
            if (mode is not null)
            {
                Line(anchorY, ModeX, mode);
            }

            for (var i = 0; i < particulars.Count; i++)
            {
                Line(firstLineY + i * 6.5, ParticularsX, particulars[i]);
            }

            if (deposit is not null)
            {
                Right(anchorY, DepositsRight, deposit);
            }

            if (withdrawal is not null)
            {
                Right(anchorY, WithdrawalsRight, withdrawal);
            }

            Right(anchorY, BalanceRight, balance);
        }

        public void Line(double y, double x, string text)
        {
            foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var width = token.Length * CharWidth;
                _words.Add(new PdfWord(token, x, x + width, y - WordHeight / 2, y + WordHeight / 2));
                x += width + CharWidth;
            }
        }

        private void Right(double y, double right, string text) =>
            Line(y, right - text.Length * CharWidth, text);

        public PdfDocumentText Build(string text) =>
            new([text], text, 1, false, new Dictionary<string, string>(), [_words]);
    }
}
