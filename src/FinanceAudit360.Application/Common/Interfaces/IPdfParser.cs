using FinanceAudit360.Domain.Enums;

namespace FinanceAudit360.Application.Common.Interfaces;

/// <summary>
/// Raw text pulled out of a PDF, page by page, plus the metadata needed to pick a parser.
/// <see cref="PageWords"/> carries word positions for parsers that must rebuild a table whose cells wrap
/// over several lines; it is empty when the extractor could not supply a layout.
/// </summary>
public sealed record PdfDocumentText(
    IReadOnlyList<string> Pages,
    string FullText,
    int PageCount,
    bool WasPasswordProtected,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<IReadOnlyList<PdfWord>>? PageWords = null)
{
    public IReadOnlyList<IReadOnlyList<PdfWord>> PageWords { get; init; } = PageWords ?? [];

    public static PdfDocumentText Empty { get; } =
        new([], string.Empty, 0, false, new Dictionary<string, string>());
}

/// <summary>One word on a page. Coordinates are in points with the origin at the top-left corner.</summary>
public sealed record PdfWord(string Text, double Left, double Right, double Top, double Bottom)
{
    public double CenterY => (Top + Bottom) / 2d;

    public double Height => Bottom - Top;
}

public sealed class PdfPasswordRequiredException(string message) : Exception(message);

public sealed class PdfPasswordIncorrectException(string message) : Exception(message);

public interface IPdfTextExtractor
{
    /// <summary>Returns true when the PDF cannot be opened without a user password.</summary>
    Task<bool> IsPasswordProtectedAsync(Stream pdfStream, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="PdfPasswordIncorrectException"/> when the supplied password is wrong.</summary>
    Task<PdfDocumentText> ExtractAsync(Stream pdfStream, string? password, CancellationToken cancellationToken = default);
}

/// <summary>One transaction line recovered from statement text, before it is matched to masters.</summary>
public sealed record ParsedTransaction
{
    public required DateTime TransactionDate { get; init; }

    public DateTime? PostingDate { get; init; }

    public required decimal Amount { get; init; }

    public required TransactionDirection Direction { get; init; }

    public TransactionType TransactionType { get; init; } = TransactionType.Other;

    public required string Description { get; init; }

    public string? ReferenceNumber { get; init; }

    public string? MerchantRawText { get; init; }

    public string? Location { get; init; }

    /// <summary>Running account balance printed on the row, when the statement carries one.</summary>
    public decimal? Balance { get; init; }

    public string? RawLine { get; init; }
}

public sealed record ParsedStatement
{
    public required BankCode BankCode { get; init; }

    public required StatementKind Kind { get; init; }

    public required string ParserName { get; init; }

    public string? CardNumberMasked { get; init; }

    public string? AccountNumberMasked { get; init; }

    public string? CardHolderName { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerEmail { get; init; }

    public string? CustomerPhone { get; init; }

    public string? AddressLine1 { get; init; }

    public string? AddressLine2 { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? PostalCode { get; init; }

    public DateTime? PeriodStart { get; init; }

    public DateTime? PeriodEnd { get; init; }

    public DateTime? StatementDate { get; init; }

    public DateTime? PaymentDueDate { get; init; }

    public decimal? OpeningBalance { get; init; }

    public decimal? ClosingBalance { get; init; }

    public decimal? MinimumDue { get; init; }

    public decimal? TotalDue { get; init; }

    public decimal? CreditLimit { get; init; }

    public decimal? AvailableCreditLimit { get; init; }

    public string Currency { get; init; } = "INR";

    public string? StatementNumber { get; init; }

    public IReadOnlyList<ParsedTransaction> Transactions { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// One parser per bank. <see cref="CanParse"/> is a cheap text probe used by the factory;
/// <see cref="Parse"/> does the expensive extraction.
/// </summary>
public interface IPdfParser
{
    string Name { get; }

    BankCode BankCode { get; }

    /// <summary>Higher priority parsers are probed first; use it to disambiguate overlapping banks.</summary>
    int Priority { get; }

    bool CanParse(PdfDocumentText document);

    ParsedStatement Parse(PdfDocumentText document);
}

public interface IPdfParserFactory
{
    IPdfParser Resolve(PdfDocumentText document);

    IPdfParser? ResolveByBank(BankCode bankCode);

    IReadOnlyList<IPdfParser> All { get; }
}

/// <summary>One check or correction applied to a parsed statement before it is written to the database.</summary>
public interface IStatementValidationRule
{
    /// <summary>Lower values run first.</summary>
    int Order { get; }

    ParsedStatement Apply(ParsedStatement statement, PdfDocumentText document);
}

/// <summary>
/// Text extraction output to validated statement: bank detection, bank-specific parser, generic fallback
/// and the validation layer, in that order.
/// </summary>
public interface IStatementParsingPipeline
{
    ParsedStatement Run(PdfDocumentText document, IPdfParser? preferredParser = null);
}

public interface IStatementImportService
{
    Task<Contracts.Statements.UploadStatementResponse> UploadAsync(
        Stream content,
        string fileName,
        long sizeInBytes,
        CancellationToken cancellationToken = default);

    Task<Contracts.Statements.StatementProcessingResultDto> ProcessAsync(
        Contracts.Statements.ProcessStatementRequest request,
        CancellationToken cancellationToken = default);
}
