using FinanceAudit360.Shared.Models;

namespace FinanceAudit360.Contracts.Reports;

public sealed record MonthlyActivityDto(
    int Year,
    int Month,
    string Label,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal Net,
    int TransactionCount);

public sealed record SpendByEntityDto(Guid? Id, string Name, decimal TotalDebit, decimal TotalCredit, decimal Net, int TransactionCount);

public sealed record CardPaymentSummaryDto(
    Guid CreditCardId,
    string CardName,
    string BankName,
    decimal TotalSpend,
    decimal TotalPayments,
    decimal CurrentOutstanding,
    int TransactionCount,
    DateTime? LastPaymentOn);

public sealed record PersonOutstandingDto(
    Guid PersonId,
    string PersonName,
    decimal TotalGiven,
    decimal TotalTaken,
    decimal Outstanding,
    DateTime? LastActivityOn);

public sealed class ReportFilterRequest
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public IReadOnlyList<Guid>? BankIds { get; set; }

    public IReadOnlyList<Guid>? CreditCardIds { get; set; }

    public IReadOnlyList<Guid>? BankAccountIds { get; set; }

    public int TopN { get; set; } = 10;
}

public sealed record AuditLogDto(
    Guid Id,
    string Action,
    string EntityName,
    string? EntityId,
    Guid? UserId,
    string? UserName,
    DateTime TimestampUtc,
    string? IpAddress,
    string? Endpoint,
    string? HttpMethod,
    int? StatusCode,
    long? DurationMs,
    string? Message,
    string? AffectedColumns);

public sealed class AuditLogFilterRequest : PaginationRequest
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public IReadOnlyList<int>? Actions { get; set; }

    public string? EntityName { get; set; }

    public Guid? UserId { get; set; }

    public string? Keyword { get; set; }
}
