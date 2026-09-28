using FinanceAudit360.Contracts.Dashboard;
using FinanceAudit360.Contracts.Persons;
using FinanceAudit360.Contracts.Reports;
using FinanceAudit360.Contracts.Transactions;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Shared.Models;

namespace FinanceAudit360.Application.Common.Interfaces;

/// <summary>Complex-query half of the hybrid repository pattern for the transaction fact table.</summary>
public interface ITransactionRepository : IGenericRepository<Transaction>
{
    Task<TransactionSearchResultDto> SearchAsync(TransactionFilterRequest filter, CancellationToken cancellationToken = default);

    Task<Transaction?> GetWithRelationsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetExistingDedupeHashesAsync(IReadOnlyList<string> hashes, CancellationToken cancellationToken = default);

    Task<DashboardTotals> GetTotalsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopEntityDto>> GetTopSpendingCardsAsync(DateTime from, DateTime to, int topN, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopEntityDto>> GetTopSpendingVendorsAsync(DateTime from, DateTime to, int topN, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopEntityDto>> GetMostActiveBanksAsync(DateTime from, DateTime to, int topN, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonthlyActivityDto>> GetMonthlyActivityAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpendByEntityDto>> GetSpendByVendorAsync(ReportFilterRequest filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpendByEntityDto>> GetSpendByCategoryAsync(ReportFilterRequest filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CardPaymentSummaryDto>> GetCardPaymentSummaryAsync(ReportFilterRequest filter, CancellationToken cancellationToken = default);

    Task<int> BulkAssignAsync(BulkAssignRequest request, string? modifiedBy, CancellationToken cancellationToken = default);
}

public sealed record DashboardTotals(
    decimal TotalCredit,
    decimal TotalDebit,
    int TransactionCount,
    int VendorCount,
    int PersonCount);

public interface IPersonRepository : IGenericRepository<Person>
{
    Task<Person?> GetWithLedgerAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<PersonDto>> SearchAsync(PersonFilterRequest filter, CancellationToken cancellationToken = default);

    Task<PagedResult<LedgerEntryDto>> GetLedgerAsync(Guid personId, PersonLedgerFilterRequest filter, CancellationToken cancellationToken = default);

    Task<PersonAuditDto?> GetAuditAsync(Guid personId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonOutstandingDto>> GetOutstandingAsync(int topN, CancellationToken cancellationToken = default);

    Task<TopEntityDto?> GetMostActivePersonAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<Person?> MatchByKeywordAsync(string narration, CancellationToken cancellationToken = default);
}

public interface ICreditCardRepository : IGenericRepository<CreditCard>
{
    Task<CreditCard?> GetWithBankAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CreditCard?> FindByLast4Async(Guid bankId, string last4, CancellationToken cancellationToken = default);

    Task<CreditCard?> FindByLast4Async(string last4, CancellationToken cancellationToken = default);

    Task<PagedResult<CreditCardDtoProjection>> SearchAsync(string? keyword, Guid? bankId, bool? isActive, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task RecalculateOutstandingAsync(Guid creditCardId, CancellationToken cancellationToken = default);
}

/// <summary>Flattened credit card row with the aggregate counts the grid needs.</summary>
public sealed record CreditCardDtoProjection(CreditCard Card, string BankName, int TransactionCount, int StatementCount);

public interface IBankAccountRepository : IGenericRepository<BankAccount>
{
    Task<BankAccount?> GetWithBankAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BankAccount?> FindByLast4Async(Guid bankId, string last4, CancellationToken cancellationToken = default);

    Task<PagedResult<BankAccountProjection>> SearchAsync(string? keyword, Guid? bankId, bool? isActive, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<TopEntityDto?> GetMostActiveAccountAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

public sealed record BankAccountProjection(BankAccount Account, string BankName, int TransactionCount);

public interface IStatementRepository : IGenericRepository<CreditCardStatement>
{
    Task<CreditCardStatement?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CreditCardStatement?> FindByDedupeKeyAsync(string dedupeKey, CancellationToken cancellationToken = default);

    Task<PagedResult<StatementProjection>> SearchAsync(Contracts.Statements.StatementFilterRequest filter, CancellationToken cancellationToken = default);
}

public sealed record StatementProjection(CreditCardStatement Statement, string BankName, string? OriginalFileName);

/// <summary>
/// Read side of the uploaded-file lifecycle. Every method works for deleted files as well as live ones,
/// because the recycle bin has to show exactly what a soft-deleted file still holds.
/// </summary>
public interface IStatementFileRepository : IGenericRepository<StatementFile>
{
    Task<PagedResult<StatementFileProjection>> SearchAsync(
        Contracts.Statements.StatementFileFilterRequest filter,
        CancellationToken cancellationToken = default);

    Task<StatementFileProjection?> GetProjectionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Contracts.Statements.StatementDto>> GetStatementsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Contracts.Statements.UploadHistoryDto>> GetUploadsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<TransactionDto>> GetTransactionsAsync(
        Guid id,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Contracts.Statements.StatementFileLookupDto>> GetLookupAsync(
        Guid? bankId,
        int? year,
        CancellationToken cancellationToken = default);
}

/// <summary>An uploaded file flattened together with the facts derived from what it imported.</summary>
public sealed record StatementFileProjection(
    StatementFile File,
    Guid? BankId,
    string? BankName,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    DateTime? StatementDate,
    int StatementCount,
    int TransactionCount,
    int UploadCount,
    string? UploadStatus,
    DateTime UploadedOnUtc,
    string? UploadedBy);


public interface IBankRepository : IGenericRepository<Bank>
{
    Task<Bank?> FindByCodeAsync(Domain.Enums.BankCode code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Bank>> GetActiveWithKeywordsAsync(CancellationToken cancellationToken = default);
}

public interface IVendorRepository : IGenericRepository<Vendor>
{
    Task<Vendor?> MatchByNarrationAsync(string narration, CancellationToken cancellationToken = default);

    Task<Vendor> GetOrCreateAsync(string name, CancellationToken cancellationToken = default);

    Task<PagedResult<VendorProjection>> SearchAsync(string? keyword, bool? isActive, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}

public sealed record VendorProjection(Vendor Vendor, string? DefaultCategoryName, int TransactionCount, decimal TotalSpend);

public interface IUploadHistoryRepository : IGenericRepository<UploadHistory>
{
    Task<PagedResult<UploadHistory>> SearchAsync(Contracts.Statements.UploadHistoryFilterRequest filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadHistory>> GetPendingAsync(int maxCount, CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);

    Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogFilterRequest filter, CancellationToken cancellationToken = default);
}
