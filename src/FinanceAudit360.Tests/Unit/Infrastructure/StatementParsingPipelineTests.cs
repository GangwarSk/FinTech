using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Infrastructure.Pdf;
using FinanceAudit360.Infrastructure.Pdf.Parsers;
using FinanceAudit360.Infrastructure.Pdf.Pipeline;
using FinanceAudit360.Infrastructure.Pdf.Pipeline.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceAudit360.Tests.Unit.Infrastructure;

/// <summary>
/// Upload pipeline after text extraction: bank detection, bank parser, generic fallback and validation.
/// </summary>
public class StatementParsingPipelineTests
{
    private const double CharWidth = 3d;
    private const double WordHeight = 6d;

    private const string DetailedHeaderText = """
        ICICI Bank Limited
        Detailed Statement
        Transaction Period: From 01/07/2025 To 31/07/2025
        Account Number: 250501507999
        """;

    [Fact]
    public void Run_ReadsIciciDetailedStatementWithWrappedHeaderAndZeroPlaceholders()
    {
        var parsed = Pipeline().Run(DetailedStatement());

        Assert.Equal("ICICI", parsed.ParserName);
        Assert.Equal(StatementKind.BankAccount, parsed.Kind);
        Assert.Equal(4, parsed.Transactions.Count);

        var salary = parsed.Transactions.Single(t => t.Description.StartsWith("NEFT-SALARY", StringComparison.Ordinal));
        Assert.Equal(TransactionDirection.Credit, salary.Direction);
        Assert.Equal(50_000m, salary.Amount);
        Assert.Equal("NEFT-SALARY JULY ACME LTD", salary.Description);

        var upi = parsed.Transactions.Single(t => t.Description.StartsWith("UPI/ZOMATO", StringComparison.Ordinal));
        Assert.Equal(TransactionDirection.Debit, upi.Direction);
        Assert.Equal(450m, upi.Amount);

        Assert.All(parsed.Transactions, t => Assert.Equal(new DateTime(2025, 7, 1).Month, t.TransactionDate.Month));
        Assert.Equal(new DateTime(2025, 7, 1), parsed.PeriodStart);
        Assert.Equal(new DateTime(2025, 7, 31), parsed.PeriodEnd);
        Assert.EndsWith("7999", parsed.AccountNumberMasked, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_TextPathDropsBroughtForwardAndFixesDirectionFromRunningBalance()
    {
        const string text = """
            ICICI BANK
            Statement of Transactions in Savings Account Number: 250501507999 in INR for the period June 25, 2025 - September 29, 2025
            25-06-2025 B/F 7,881.45
            27-06-2025 UPI/SUMIT KUMA/Payment 379.00 7,502.45
            30-06-2025 250501507999:Int.Pd:29-03-2025 to 29-06-2025 343.00 7,845.45
            """;
        var document = new PdfDocumentText([text], text, 1, false, new Dictionary<string, string>());

        var parsed = Pipeline().Run(document);

        Assert.Equal(2, parsed.Transactions.Count);
        Assert.DoesNotContain(parsed.Transactions, t => t.Description.Contains("B/F", StringComparison.Ordinal));

        Assert.Equal(TransactionDirection.Debit, parsed.Transactions[0].Direction);
        var interest = parsed.Transactions[1];
        Assert.Equal(TransactionDirection.Credit, interest.Direction);
        Assert.Equal(343m, interest.Amount);
    }

    [Fact]
    public void Run_ReportsBalancesThatDoNotReconcile()
    {
        var statement = new ParsedStatement
        {
            BankCode = BankCode.Icici,
            Kind = StatementKind.BankAccount,
            ParserName = "ICICI",
            OpeningBalance = 100m,
            ClosingBalance = 500m,
            Transactions =
            [
                new ParsedTransaction
                {
                    TransactionDate = new DateTime(2025, 7, 2),
                    Amount = 50m,
                    Direction = TransactionDirection.Credit,
                    Description = "Deposit"
                }
            ]
        };

        var validated = new ReconciliationRule().Apply(statement, PdfDocumentText.Empty);

        Assert.Contains(validated.Warnings, w => w.StartsWith("Balances do not reconcile", StringComparison.Ordinal));
    }

    private static StatementParsingPipeline Pipeline()
    {
        IPdfParser[] parsers = [new IciciPdfParser(), new HdfcPdfParser(), new GenericPdfParser()];
        var factory = new PdfParserFactory(parsers, NullLogger<PdfParserFactory>.Instance);
        IStatementValidationRule[] rules =
            [new ReconciliationRule(), new BalanceRowFilterRule(), new RunningBalanceDirectionRule()];

        return new StatementParsingPipeline(factory, rules, NullLogger<StatementParsingPipeline>.Instance);
    }

    /// <summary>
    /// ICICI net-banking layout: withdrawals before deposits, 0.00 in the unused column, two dates per row,
    /// and header captions wrapped over three lines.
    /// </summary>
    private static PdfDocumentText DetailedStatement()
    {
        var words = new List<PdfWord>();

        void Line(double y, double x, string text)
        {
            foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var width = token.Length * CharWidth;
                words.Add(new PdfWord(token, x, x + width, y - WordHeight / 2, y + WordHeight / 2));
                x += width + CharWidth;
            }
        }

        void Right(double y, double right, string text) => Line(y, right - text.Length * CharWidth, text);

        const double SNo = 40, Value = 60, Txn = 110, Cheque = 160, Remarks = 210;
        const double WithdrawalRight = 470, DepositRight = 540, BalanceRight = 610;

        Line(100, SNo, "S No.");
        Line(100, Value, "Value");
        Line(100, Txn, "Transaction");
        Line(100, Cheque, "Cheque");
        Line(100, Remarks, "Transaction");
        Right(100, WithdrawalRight, "Withdrawal");
        Right(100, DepositRight, "Deposit");
        Right(100, BalanceRight, "Balance");
        Line(107, Value, "Date");
        Line(107, Txn, "Date");
        Line(107, Cheque, "Number");
        Line(107, Remarks, "Remarks");
        Right(107, WithdrawalRight, "Amount");
        Right(107, DepositRight, "Amount");
        Right(107, BalanceRight, "(INR )");
        Right(114, WithdrawalRight, "(INR )");
        Right(114, DepositRight, "(INR )");

        void Row(double y, string no, string date, string[] remarks, string withdrawal, string deposit, string balance)
        {
            Line(y, SNo, no);
            Line(y, Value, date);
            Line(y, Txn, date);
            Line(y, Cheque, "-");
            for (var i = 0; i < remarks.Length; i++)
            {
                Line(y + i * 6.5, Remarks, remarks[i]);
            }

            Right(y, WithdrawalRight, withdrawal);
            Right(y, DepositRight, deposit);
            Right(y, BalanceRight, balance);
        }

        Row(130, "1", "01/07/2025", ["NEFT-SALARY JULY", "ACME LTD"], "0.00", "50,000.00", "57,881.45");
        Row(150, "2", "03/07/2025", ["UPI/ZOMATO/Payment"], "450.00", "0.00", "57,431.45");
        Row(165, "3", "10/07/2025", ["ATM/CASH WDL/NOIDA", "SECTOR 18"], "2,000.00", "0.00", "55,431.45");
        Row(185, "4", "31/07/2025", ["250501507999:Int.Pd"], "0.00", "120.00", "55,551.45");

        return new PdfDocumentText([DetailedHeaderText], DetailedHeaderText, 1, false, new Dictionary<string, string>(), [words]);
    }
}
