using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Infrastructure.Pdf;
using FinanceAudit360.Infrastructure.Pdf.Parsers;
using FinanceAudit360.Infrastructure.Pdf.Parsing;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceAudit360.Tests.Unit.Infrastructure;

public class StatementParserTests
{
    private const string HdfcStatement = """
        HDFC BANK CREDIT CARD STATEMENT
        Name: RAJESH SHARMA
        Card Number: XXXX XXXX XXXX 4321
        Statement Period: 01/05/2024 to 31/05/2024
        Statement Date: 31/05/2024
        Payment Due Date: 18/06/2024
        Opening Balance: 12,500.00
        Total Amount Due: 45,320.75
        Minimum Amount Due: 2,266.00
        Credit Limit: 300,000.00
        Available Credit Limit: 254,679.25
        Email: rajesh.sharma@example.com
        Mobile: 9876543210
        Address: 12 MG ROAD BANGALORE 560001
        02/05/2024 03/05/2024 SWIGGY BANGALORE IN 845.00
        05/05/2024 06/05/2024 AMAZON RETAIL BENGALURU 3,250.50
        10/05/2024 11/05/2024 PAYMENT RECEIVED THANK YOU 12,500.00 CR
        15/05/2024 16/05/2024 ANNUAL FEE 500.00
        20/05/2024 21/05/2024 ATM CASH WDL MG ROAD 5,000.00
        """;

    private const string IciciStatement = """
        ICICI BANK Credit Card Statement
        Card Number: XXXXXXXXXXXX9876
        Statement Period: 01/06/2024 - 30/06/2024
        Total Amount due: 18,200.00
        Minimum Amount Due: 910.00
        Payment Due Date: 18/07/2024
        03/06/2024 UBER INDIA SYSTEMS 420.00
        08/06/2024 NETFLIX SUBSCRIPTION 649.00
        12/06/2024 REFUND MYNTRA 1,299.00 CR
        """;

    private const string SbiStatement = """
        SBI CARD Statement
        Card Number: XXXX XXXX XXXX 1111
        Statement Period: 01/07/2024 to 31/07/2024
        Total Amount Due: 9,000.00
        03/07/2024 BIGBASKET BANGALORE 1,250.00 D
        09/07/2024 PAYMENT RECEIVED 4,000.00 C
        """;

    private static PdfDocumentText Document(string text) =>
        new([text], text, 1, false, new Dictionary<string, string>());

    [Fact]
    public void HdfcParser_ClaimsAnHdfcDocument() =>
        Assert.True(new HdfcPdfParser().CanParse(Document(HdfcStatement)));

    [Fact]
    public void HdfcParser_DoesNotClaimAnIciciDocument() =>
        Assert.False(new HdfcPdfParser().CanParse(Document(IciciStatement)));

    [Fact]
    public void HdfcParser_ExtractsStatementMetadata()
    {
        var parsed = new HdfcPdfParser().Parse(Document(HdfcStatement));

        Assert.Equal(BankCode.Hdfc, parsed.BankCode);
        Assert.Equal(StatementKind.CreditCard, parsed.Kind);
        Assert.Equal(new DateTime(2024, 5, 1), parsed.PeriodStart);
        Assert.Equal(new DateTime(2024, 5, 31), parsed.PeriodEnd);
        Assert.Equal(new DateTime(2024, 6, 18), parsed.PaymentDueDate);
        Assert.Equal(DateTimeKind.Utc, parsed.PeriodStart!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, parsed.PeriodEnd!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, parsed.PaymentDueDate!.Value.Kind);
        Assert.Equal(45_320.75m, parsed.TotalDue);
        Assert.Equal(2_266.00m, parsed.MinimumDue);
        Assert.Equal("rajesh.sharma@example.com", parsed.CustomerEmail);
        Assert.Equal("9876543210", parsed.CustomerPhone);
        Assert.EndsWith("4321", parsed.CardNumberMasked, StringComparison.Ordinal);
    }

    [Fact]
    public void HdfcParser_ExtractsEveryTransactionRow()
    {
        var parsed = new HdfcPdfParser().Parse(Document(HdfcStatement));

        Assert.Equal(5, parsed.Transactions.Count);

        var swiggy = parsed.Transactions.First(t => t.Description.Contains("SWIGGY", StringComparison.Ordinal));
        Assert.Equal(new DateTime(2024, 5, 2), swiggy.TransactionDate);
        Assert.Equal(new DateTime(2024, 5, 3), swiggy.PostingDate);
        Assert.Equal(DateTimeKind.Utc, swiggy.TransactionDate.Kind);
        Assert.Equal(DateTimeKind.Utc, swiggy.PostingDate!.Value.Kind);
        Assert.Equal(845.00m, swiggy.Amount);
        Assert.Equal(TransactionDirection.Debit, swiggy.Direction);
    }

    [Fact]
    public void HdfcParser_MarksPaymentLinesAsCredits()
    {
        var parsed = new HdfcPdfParser().Parse(Document(HdfcStatement));
        var payment = parsed.Transactions.First(t => t.Description.Contains("PAYMENT", StringComparison.Ordinal));

        Assert.Equal(TransactionDirection.Credit, payment.Direction);
        Assert.Equal(12_500.00m, payment.Amount);
        Assert.Equal(TransactionType.Payment, payment.TransactionType);
    }

    [Fact]
    public void HdfcParser_ClassifiesCashWithdrawalsAndFees()
    {
        var parsed = new HdfcPdfParser().Parse(Document(HdfcStatement));

        Assert.Equal(TransactionType.CashWithdrawal,
            parsed.Transactions.First(t => t.Description.Contains("ATM", StringComparison.Ordinal)).TransactionType);

        Assert.Equal(TransactionType.Fees,
            parsed.Transactions.First(t => t.Description.Contains("ANNUAL FEE", StringComparison.Ordinal)).TransactionType);
    }

    [Fact]
    public void IciciParser_HandlesCrSuffixAsCredit()
    {
        var parsed = new IciciPdfParser().Parse(Document(IciciStatement));
        var refund = parsed.Transactions.First(t => t.Description.Contains("MYNTRA", StringComparison.Ordinal));

        Assert.Equal(TransactionDirection.Credit, refund.Direction);
        Assert.Equal(1_299.00m, refund.Amount);
        Assert.DoesNotContain("CR", refund.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void SbiParser_HandlesTrailingDebitCreditFlags()
    {
        var parsed = new SbiPdfParser().Parse(Document(SbiStatement));

        Assert.Equal(TransactionDirection.Debit,
            parsed.Transactions.First(t => t.Description.Contains("BIGBASKET", StringComparison.Ordinal)).Direction);

        Assert.Equal(TransactionDirection.Credit,
            parsed.Transactions.First(t => t.Description.Contains("PAYMENT", StringComparison.Ordinal)).Direction);
    }

    [Fact]
    public void ParserFactory_PicksTheIssuerSpecificParser()
    {
        var factory = BuildFactory();

        Assert.Equal(BankCode.Hdfc, factory.Resolve(Document(HdfcStatement)).BankCode);
        Assert.Equal(BankCode.Icici, factory.Resolve(Document(IciciStatement)).BankCode);
        Assert.Equal(BankCode.Sbi, factory.Resolve(Document(SbiStatement)).BankCode);
    }

    [Fact]
    public void ParserFactory_FallsBackToTheGenericParser()
    {
        var factory = BuildFactory();
        var unknown = Document("UNKNOWN BANK\nStatement Period: 01/01/2024 to 31/01/2024\n05/01/2024 SOME MERCHANT 100.00");

        Assert.Equal(BankCode.Other, factory.Resolve(unknown).BankCode);
    }

    [Fact]
    public void ParserFactory_ResolvesByBankCode() =>
        Assert.Equal("Axis", BuildFactory().ResolveByBank(BankCode.Axis)!.Name);

    [Theory]
    [InlineData("HDFC Bank Credit Card Statement", BankCode.Hdfc, StatementKind.CreditCard)]
    [InlineData("ICICI Bank Credit Card Statement", BankCode.Icici, StatementKind.CreditCard)]
    [InlineData("CitiBank Credit Card Statement", BankCode.Citi, StatementKind.CreditCard)]
    [InlineData("OneCard South Indian Bank", BankCode.SouthIndian, StatementKind.CreditCard)]
    [InlineData("Uni Card Statement", BankCode.UniCard, StatementKind.CreditCard)]
    [InlineData("Slice Card Statement", BankCode.Slice, StatementKind.CreditCard)]
    [InlineData("SBI Card Credit Card Statement", BankCode.Sbi, StatementKind.CreditCard)]
    [InlineData("Axis Bank Credit Card Statement", BankCode.Axis, StatementKind.CreditCard)]
    [InlineData("IDFC First Bank Credit Card Statement", BankCode.IdfcFirst, StatementKind.CreditCard)]
    [InlineData("Paytm PostPaid Statement", BankCode.PaytmPostPaid, StatementKind.CreditCard)]
    [InlineData("Uttar Pradesh Gramin Bank Savings Account Statement", BankCode.UttarPradeshGramin, StatementKind.BankAccount)]
    [InlineData("Punjab National Bank Savings Account Statement", BankCode.PunjabNational, StatementKind.BankAccount)]
    [InlineData("ICICI Bank Savings Account Statement", BankCode.Icici, StatementKind.BankAccount)]
    [InlineData("Axis Bank Savings Account Statement", BankCode.Axis, StatementKind.BankAccount)]
    [InlineData("Kotak Mahindra Bank Savings Account Statement", BankCode.Kotak, StatementKind.BankAccount)]
    [InlineData("Airtel Payments Bank Savings Account Statement", BankCode.AirtelPayments, StatementKind.BankAccount)]
    public void ParserFactory_ResolvesRequestedIssuerAndStatementKind(
        string header,
        BankCode expectedBank,
        StatementKind expectedKind)
    {
        var document = Document(header);

        var parsed = BuildFactory().Resolve(document).Parse(document);

        Assert.Equal(expectedBank, parsed.BankCode);
        Assert.Equal(expectedKind, parsed.Kind);
    }

    [Theory]
    [InlineData("02/05/2024", 2024, 5, 2)]
    [InlineData("02-05-2024", 2024, 5, 2)]
    [InlineData("02 May 2024", 2024, 5, 2)]
    [InlineData("2024-05-02", 2024, 5, 2)]
    [InlineData("02-May-24", 2024, 5, 2)]
    public void StatementTextParser_ParsesIndianDateFormats(string value, int year, int month, int day)
    {
        var parsed = StatementTextParser.ParseDate(value);

        Assert.Equal(new DateTime(year, month, day), parsed);
        Assert.Equal(DateTimeKind.Utc, parsed!.Value.Kind);
    }

    [Fact]
    public void HdfcParser_DerivesUtcPeriodStartWhenOnlyStatementDateExists()
    {
        var document = Document("HDFC BANK CREDIT CARD STATEMENT\nStatement Date: 31/05/2024\n02/05/2024 SWIGGY 845.00");

        var parsed = new HdfcPdfParser().Parse(document);

        Assert.Equal(DateTimeKind.Utc, parsed.PeriodStart!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, parsed.PeriodEnd!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, parsed.Transactions[0].TransactionDate.Kind);
    }

    [Theory]
    [InlineData("1,23,456.78", 123456.78)]
    [InlineData("1,234.50", 1234.50)]
    [InlineData("500.00", 500)]
    public void StatementTextParser_ParsesIndianAmountFormatting(string value, decimal expected) =>
        Assert.Equal(expected, StatementTextParser.ParseAmount(value));

    private static PdfParserFactory BuildFactory() =>
        new(
        [
            new HdfcPdfParser(),
            new IciciPdfParser(),
            new CitiPdfParser(),
            new SbiPdfParser(),
            new AxisPdfParser(),
            new KotakPdfParser(),
            new IndusIndPdfParser(),
            new IdfcFirstPdfParser(),
            new SouthIndianBankPdfParser(),
            new UniCardPdfParser(),
            new SlicePdfParser(),
            new PunjabNationalBankPdfParser(),
            new UttarPradeshGraminBankPdfParser(),
            new AirtelPaymentsBankPdfParser(),
            new PaytmPostPaidPdfParser(),
            new StandardCharteredPdfParser(),
            new AmexPdfParser(),
            new GenericPdfParser()
        ], NullLogger<PdfParserFactory>.Instance);
}
