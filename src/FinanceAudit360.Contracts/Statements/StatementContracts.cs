using FinanceAudit360.Contracts.Persons;
using FinanceAudit360.Shared.Models;

namespace FinanceAudit360.Contracts.Statements;

public sealed record UploadStatementResponse(
    Guid UploadId,
    Guid? StatementFileId,
    string Status,
    bool RequiresPassword,
    string? DetectedBank,
    string? DetectedKind,
    string? Message);

public sealed record ProcessStatementRequest(Guid UploadId, string? Password, Guid? OverrideBankId, bool AllowDuplicate = false);

public sealed record StatementProcessingResultDto(
    Guid UploadId,
    Guid? StatementId,
    string Status,
    string? BankName,
    string? StatementKind,
    string? ParserName,
    string? CardNumberMasked,
    string? AccountNumberMasked,
    string? CardHolderName,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    AddressDto? BillingAddress,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    DateTime? StatementDate,
    DateTime? PaymentDueDate,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    decimal? MinimumDue,
    decimal? TotalDue,
    int TransactionsExtracted,
    int TransactionsImported,
    int TransactionsSkipped,
    long DurationMs,
    IReadOnlyList<string> Warnings,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record StatementDto(
    Guid Id,
    Guid BankId,
    string BankName,
    string Kind,
    string Status,
    string? StatementNumber,
    string? CardNumberMasked,
    string? AccountNumberMasked,
    string? CardHolderName,
    string? CustomerName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime? StatementDate,
    DateTime? PaymentDueDate,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal? MinimumDue,
    decimal? TotalDue,
    decimal TotalCredits,
    decimal TotalDebits,
    int TransactionCount,
    string Currency,
    string? ParserName,
    Guid? StatementFileId,
    string? OriginalFileName,
    DateTime CreatedOnUtc);

public sealed class StatementFilterRequest : PaginationRequest
{
    public IReadOnlyList<Guid>? BankIds { get; set; }

    public IReadOnlyList<Guid>? CreditCardIds { get; set; }

    public IReadOnlyList<Guid>? BankAccountIds { get; set; }

    public DateTime? PeriodFrom { get; set; }

    public DateTime? PeriodTo { get; set; }

    public string? Keyword { get; set; }
}

public sealed record UploadHistoryDto(
    Guid Id,
    string FileName,
    long SizeInBytes,
    string Status,
    string? DetectedBank,
    string? DetectedKind,
    string? ParserName,
    bool RequiresPassword,
    int TransactionsExtracted,
    int TransactionsImported,
    int TransactionsSkipped,
    DateTime StartedOnUtc,
    DateTime? CompletedOnUtc,
    long DurationMs,
    string? ErrorCode,
    string? ErrorMessage,
    Guid? StatementId,
    Guid? StatementFileId,
    string? CreatedBy);

public sealed class UploadHistoryFilterRequest : PaginationRequest
{
    public IReadOnlyList<int>? Statuses { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string? Keyword { get; set; }
}

/// <summary>
/// One uploaded PDF together with everything it produced. The file is the lifecycle root: deleting it
/// takes its upload-history rows, its statement and every imported transaction with it.
/// </summary>
public sealed record StatementFileSummaryDto(
    Guid Id,
    string OriginalFileName,
    string DisplayName,
    long SizeInBytes,
    Guid? BankId,
    string? BankName,
    string? DetectedBank,
    string? DetectedKind,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    DateTime? StatementDate,
    int StatementCount,
    int TransactionCount,
    int UploadCount,
    string? UploadStatus,
    DateTime UploadedOnUtc,
    string? UploadedBy,
    bool IsDeleted,
    DateTime? DeletedOnUtc,
    string? DeletedBy);

public sealed record StatementFileDetailDto(
    StatementFileSummaryDto File,
    bool IsPasswordProtected,
    int PageCount,
    string ContentHash,
    IReadOnlyList<StatementDto> Statements,
    IReadOnlyList<UploadHistoryDto> Uploads);

/// <summary>Counts shown in the delete confirmation so the admin sees exactly what goes with the file.</summary>
public sealed record StatementFileImpactDto(
    Guid Id,
    string OriginalFileName,
    string DisplayName,
    int StatementCount,
    int TransactionCount,
    int UploadCount,
    bool IsDeleted);

/// <summary>
/// Cascading dropdown option. <see cref="PeriodStart"/> and <see cref="PeriodEnd"/> let the client build
/// the year level without a second round trip.
/// </summary>
public sealed record StatementFileLookupDto(
    Guid Id,
    string Label,
    string OriginalFileName,
    Guid? BankId,
    string? BankName,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    int TransactionCount);

public sealed class StatementFileFilterRequest : PaginationRequest
{
    public IReadOnlyList<Guid>? BankIds { get; set; }

    public DateTime? PeriodFrom { get; set; }

    public DateTime? PeriodTo { get; set; }

    public string? Keyword { get; set; }

    /// <summary>True for the recycle bin, which shows only soft-deleted files.</summary>
    public bool DeletedOnly { get; set; }
}

/// <summary>Pagination for the "what did this file import?" table in the file detail dialog.</summary>
public sealed class StatementFileTransactionFilter : PaginationRequest;
