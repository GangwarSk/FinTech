using FinanceAudit360.Shared.Models;

namespace FinanceAudit360.Contracts.Persons;

public sealed record AddressDto(
    string? Line1,
    string? Line2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country);

public sealed record PersonDto(
    Guid Id,
    string Name,
    string? Mobile,
    string? Email,
    AddressDto Address,
    string? Relationship,
    string? Notes,
    string? MatchKeywords,
    bool IsActive,
    decimal TotalGiven,
    decimal TotalTaken,
    decimal OutstandingBalance,
    DateTime? LastActivityOn,
    int LedgerEntryCount,
    DateTime CreatedOnUtc);

public sealed class PersonFilterRequest : PaginationRequest
{
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public bool? HasOutstanding { get; set; }

    public decimal? MinOutstanding { get; set; }

    public decimal? MaxOutstanding { get; set; }
}

public sealed record CreatePersonRequest(
    string Name,
    string? Mobile,
    string? Email,
    AddressDto? Address,
    string? Relationship,
    string? Notes,
    string? MatchKeywords);

public sealed record UpdatePersonRequest(
    string Name,
    string? Mobile,
    string? Email,
    AddressDto? Address,
    string? Relationship,
    string? Notes,
    string? MatchKeywords,
    bool IsActive);

public sealed record LedgerEntryDto(
    Guid Id,
    Guid PersonId,
    DateTime EntryDate,
    decimal Amount,
    decimal SignedAmount,
    string Currency,
    string EntryType,
    int EntryTypeValue,
    string? Description,
    string? ReferenceNumber,
    Guid? TransactionId,
    Guid? BankAccountId,
    string? BankAccountName,
    Guid? CreditCardId,
    string? CreditCardName,
    bool IsSettled,
    DateTime? SettledOn,
    DateTime CreatedOnUtc);

public sealed record CreateLedgerEntryRequest(
    DateTime EntryDate,
    decimal Amount,
    int EntryType,
    string? Description,
    string? ReferenceNumber,
    Guid? TransactionId,
    Guid? BankAccountId,
    Guid? CreditCardId);

public sealed class PersonLedgerFilterRequest : PaginationRequest
{
    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public decimal? AmountFrom { get; set; }

    public decimal? AmountTo { get; set; }

    public IReadOnlyList<int>? EntryTypes { get; set; }

    public Guid? BankAccountId { get; set; }

    public Guid? CreditCardId { get; set; }

    public string? Keyword { get; set; }
}

public sealed record PersonAuditDto(
    PersonDto Person,
    decimal TotalGiven,
    decimal TotalTaken,
    decimal Balance,
    decimal SettlementsReceived,
    decimal SettlementsPaid,
    int TransactionCount,
    IReadOnlyList<LedgerEntryDto> RecentEntries,
    IReadOnlyList<PersonTimelinePointDto> Timeline);

public sealed record PersonTimelinePointDto(string Period, decimal Given, decimal Taken, decimal RunningBalance);
