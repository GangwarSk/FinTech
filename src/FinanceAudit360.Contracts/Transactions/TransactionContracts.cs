using FinanceAudit360.Shared.Models;

namespace FinanceAudit360.Contracts.Transactions;

public sealed record TransactionDto(
    Guid Id,
    DateTime TransactionDate,
    DateTime PostingDate,
    decimal Amount,
    decimal CreditAmount,
    decimal DebitAmount,
    decimal SignedAmount,
    string Currency,
    string Direction,
    string TransactionType,
    string Source,
    string Description,
    string? ReferenceNumber,
    string? MerchantRawText,
    string? Location,
    string? Notes,
    Guid? VendorId,
    string? VendorName,
    Guid? CategoryId,
    string? CategoryName,
    Guid? BankId,
    string? BankName,
    Guid? BankAccountId,
    string? BankAccountName,
    Guid? CreditCardId,
    string? CreditCardName,
    Guid? PersonId,
    string? PersonName,
    Guid? StatementId,
    string? StatementPeriod,
    Guid? StatementFileId,
    string? SourceFileName,
    bool IsReconciled,
    bool IsRecurring,
    bool IsDisputed,
    DateTime CreatedOnUtc,
    string? CreatedBy,
    DateTime? ModifiedOnUtc,
    string? ModifiedBy);

/// <summary>
/// Server-side filter for the Transaction Explorer grid. Every collection filter is an OR within
/// itself and an AND across different filters; <see cref="Keyword"/> performs a global OR search.
/// </summary>
public sealed class TransactionFilterRequest : PaginationRequest
{
    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public DateTime? PostingDateFrom { get; set; }

    public DateTime? PostingDateTo { get; set; }

    public decimal? AmountFrom { get; set; }

    public decimal? AmountTo { get; set; }

    public bool? CreditOnly { get; set; }

    public bool? DebitOnly { get; set; }

    public IReadOnlyList<Guid>? BankIds { get; set; }

    public IReadOnlyList<Guid>? CreditCardIds { get; set; }

    public IReadOnlyList<Guid>? BankAccountIds { get; set; }

    public IReadOnlyList<Guid>? VendorIds { get; set; }

    public IReadOnlyList<Guid>? PersonIds { get; set; }

    public IReadOnlyList<Guid>? CategoryIds { get; set; }

    public IReadOnlyList<Guid>? StatementIds { get; set; }

    /// <summary>Narrows to the transactions imported from specific uploaded PDFs, resolved via their statement.</summary>
    public IReadOnlyList<Guid>? StatementFileIds { get; set; }

    public IReadOnlyList<int>? TransactionTypes { get; set; }

    public string? Keyword { get; set; }

    public string? ReferenceNumber { get; set; }

    public bool? IsReconciled { get; set; }

    public bool? IsDisputed { get; set; }

    public bool? IsRecurring { get; set; }

    /// <summary>Optional grouping key applied server-side: vendor, person, card, account, bank, category, month, type.</summary>
    public string? GroupBy { get; set; }
}

public sealed record TransactionGroupDto(string Key, string Label, int Count, decimal TotalCredit, decimal TotalDebit, decimal Net);

public sealed record TransactionSearchResultDto(
    IReadOnlyList<TransactionDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal NetAmount,
    IReadOnlyList<TransactionGroupDto> Groups);

public sealed record CreateTransactionRequest(
    DateTime TransactionDate,
    DateTime? PostingDate,
    decimal Amount,
    int Direction,
    int TransactionType,
    string Description,
    string? ReferenceNumber,
    string? Notes,
    Guid? BankId,
    Guid? BankAccountId,
    Guid? CreditCardId,
    Guid? VendorId,
    Guid? CategoryId,
    Guid? PersonId,
    string? Currency);

public sealed record UpdateTransactionRequest(
    DateTime TransactionDate,
    DateTime? PostingDate,
    decimal Amount,
    int Direction,
    int TransactionType,
    string Description,
    string? ReferenceNumber,
    string? Notes,
    Guid? BankId,
    Guid? BankAccountId,
    Guid? CreditCardId,
    Guid? VendorId,
    Guid? CategoryId,
    Guid? PersonId,
    bool IsReconciled,
    bool IsRecurring,
    bool IsDisputed);

public sealed record BulkAssignRequest(
    IReadOnlyList<Guid> TransactionIds,
    Guid? VendorId,
    Guid? CategoryId,
    Guid? PersonId);
