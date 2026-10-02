using FinanceAudit360.Application.Common.Extensions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Application.Features.Statements;
using FinanceAudit360.Contracts.Dashboard;
using FinanceAudit360.Contracts.Statements;
using FinanceAudit360.Contracts.Transactions;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Extensions;
using FinanceAudit360.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Persistence.Repositories;

public sealed class BankRepository(ApplicationDbContext context) : GenericRepository<Bank>(context), IBankRepository
{
    public Task<Bank?> FindByCodeAsync(BankCode code, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(b => b.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Bank>> GetActiveWithKeywordsAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(b => b.IsActive).ToListAsync(cancellationToken);
}

public sealed class CreditCardRepository(ApplicationDbContext context)
    : GenericRepository<CreditCard>(context), ICreditCardRepository
{
    public Task<CreditCard?> GetWithBankAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().Include(c => c.Bank).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<CreditCard?> FindByLast4Async(Guid bankId, string last4, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(c => c.BankId == bankId && c.CardNumber.Last4 == last4, cancellationToken);

    public Task<CreditCard?> FindByLast4Async(string last4, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(c => c.CardNumber.Last4 == last4, cancellationToken);

    public async Task<PagedResult<CreditCardDtoProjection>> SearchAsync(
        string? keyword,
        Guid? bankId,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var upper = keyword?.Trim().ToUpperInvariant();

        var query = Set.AsNoTracking()
            .WhereIf(bankId.HasValue, c => c.BankId == bankId!.Value)
            .WhereIf(isActive.HasValue, c => c.IsActive == isActive!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(upper),
                c => c.CardHolderName.ToUpper().Contains(upper!) ||
                     c.CardNumber.Last4.Contains(upper!) ||
                     (c.Nickname != null && c.Nickname.ToUpper().Contains(upper!)) ||
                     (c.ProductName != null && c.ProductName.ToUpper().Contains(upper!)))
            .OrderBy(c => c.Nickname ?? c.CardHolderName)
            .Select(c => new CreditCardDtoProjection(
                c,
                c.Bank!.Name,
                Context.Transactions.Count(t => t.CreditCardId == c.Id),
                Context.Statements.Count(s => s.CreditCardId == c.Id)));

        return await query.ToPagedResultAsync(pageNumber, pageSize, cancellationToken);
    }

    /// <summary>Recomputes the outstanding balance from the card's own transaction history.</summary>
    public async Task RecalculateOutstandingAsync(Guid creditCardId, CancellationToken cancellationToken = default)
    {
        var card = await Set.FirstOrDefaultAsync(c => c.Id == creditCardId, cancellationToken);
        if (card is null)
        {
            return;
        }

        var totals = await Context.Transactions
            .AsNoTracking()
            .Where(t => t.CreditCardId == creditCardId)
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(t => t.DebitAmount), Credit = g.Sum(t => t.CreditAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        card.SyncOutstanding((totals?.Debit ?? 0m) - (totals?.Credit ?? 0m), DateTime.UtcNow);
    }
}

public sealed class BankAccountRepository(ApplicationDbContext context)
    : GenericRepository<BankAccount>(context), IBankAccountRepository
{
    public Task<BankAccount?> GetWithBankAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().Include(a => a.Bank).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<BankAccount?> FindByLast4Async(Guid bankId, string last4, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(a => a.BankId == bankId && a.AccountNumberLast4 == last4, cancellationToken);

    public async Task<PagedResult<BankAccountProjection>> SearchAsync(
        string? keyword,
        Guid? bankId,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var upper = keyword?.Trim().ToUpperInvariant();

        var query = Set.AsNoTracking()
            .WhereIf(bankId.HasValue, a => a.BankId == bankId!.Value)
            .WhereIf(isActive.HasValue, a => a.IsActive == isActive!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(upper),
                a => a.AccountHolderName.ToUpper().Contains(upper!) ||
                     a.AccountNumberLast4.Contains(upper!) ||
                     (a.Nickname != null && a.Nickname.ToUpper().Contains(upper!)) ||
                     (a.BranchName != null && a.BranchName.ToUpper().Contains(upper!)))
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => a.Nickname ?? a.AccountHolderName)
            .Select(a => new BankAccountProjection(
                a,
                a.Bank!.Name,
                Context.Transactions.Count(t => t.BankAccountId == a.Id)));

        return await query.ToPagedResultAsync(pageNumber, pageSize, cancellationToken);
    }

    public async Task<TopEntityDto?> GetMostActiveAccountAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var row = await Context.Transactions
            .AsNoTracking()
            .Where(t => t.BankAccountId != null && t.TransactionDate >= from && t.TransactionDate <= to)
            .GroupBy(t => t.BankAccountId!.Value)
            .Select(g => new { Id = g.Key, Amount = g.Sum(t => t.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var name = await Set.AsNoTracking()
            .Where(a => a.Id == row.Id)
            .Select(a => a.Nickname ?? "A/c ****" + a.AccountNumberLast4)
            .FirstOrDefaultAsync(cancellationToken);

        return new TopEntityDto(row.Id, name ?? "Unknown account", row.Amount, row.Count);
    }
}

public sealed class StatementRepository(ApplicationDbContext context)
    : GenericRepository<CreditCardStatement>(context), IStatementRepository
{
    public Task<CreditCardStatement?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .Include(s => s.Bank)
            .Include(s => s.CreditCard)
            .Include(s => s.BankAccount)
            .Include(s => s.StatementFile)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<CreditCardStatement?> FindByDedupeKeyAsync(string dedupeKey, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(s => s.DedupeKey == dedupeKey, cancellationToken);

    public async Task<PagedResult<StatementProjection>> SearchAsync(
        StatementFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = Set.AsNoTracking()
            .WhereIf(filter.BankIds is { Count: > 0 }, s => filter.BankIds!.Contains(s.BankId))
            .WhereIf(filter.CreditCardIds is { Count: > 0 }, s => s.CreditCardId.HasValue && filter.CreditCardIds!.Contains(s.CreditCardId.Value))
            .WhereIf(filter.BankAccountIds is { Count: > 0 }, s => s.BankAccountId.HasValue && filter.BankAccountIds!.Contains(s.BankAccountId.Value))
            .WhereIf(filter.PeriodFrom.HasValue, s => s.PeriodEnd >= filter.PeriodFrom!.Value)
            .WhereIf(filter.PeriodTo.HasValue, s => s.PeriodStart <= filter.PeriodTo!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword),
                s => (s.CardHolderName != null && s.CardHolderName.ToUpper().Contains(keyword!)) ||
                     (s.CustomerName != null && s.CustomerName.ToUpper().Contains(keyword!)) ||
                     (s.StatementNumber != null && s.StatementNumber.ToUpper().Contains(keyword!)) ||
                     (s.AccountNumberMasked != null && s.AccountNumberMasked.Contains(keyword!)))
            .ApplySort(filter.SortBy, filter.SortDirection, nameof(CreditCardStatement.PeriodEnd))
            .Select(s => new StatementProjection(
                s,
                s.Bank!.Name,
                s.StatementFile != null ? s.StatementFile.OriginalFileName : null));

        return await query.ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);
    }
}

public sealed class VendorRepository(ApplicationDbContext context) : GenericRepository<Vendor>(context), IVendorRepository
{
    public async Task<Vendor?> MatchByNarrationAsync(string narration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(narration))
        {
            return null;
        }

        var upper = narration.ToUpperInvariant();

        var exact = await Set.FirstOrDefaultAsync(v => v.IsActive && upper.Contains(v.NormalizedName), cancellationToken);
        if (exact is not null)
        {
            return exact;
        }

        var candidates = await Set.AsNoTracking()
            .Where(v => v.IsActive && v.MatchKeywords != null && v.MatchKeywords != "")
            .Select(v => new { v.Id, v.MatchKeywords })
            .ToListAsync(cancellationToken);

        var matchId = candidates
            .Where(c => c.MatchKeywords!
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(token => upper.Contains(token.ToUpperInvariant(), StringComparison.Ordinal)))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefault();

        return matchId is null ? null : await Set.FirstOrDefaultAsync(v => v.Id == matchId, cancellationToken);
    }

    public async Task<Vendor> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.NormalizeText().ToUpperInvariant();

        var existing = await Set.FirstOrDefaultAsync(v => v.NormalizedName == normalized, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var vendor = Vendor.Create(name);
        await Set.AddAsync(vendor, cancellationToken);
        return vendor;
    }

    public async Task<PagedResult<VendorProjection>> SearchAsync(
        string? keyword,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var upper = keyword?.Trim().ToUpperInvariant();

        var query = Set.AsNoTracking()
            .WhereIf(isActive.HasValue, v => v.IsActive == isActive!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(upper),
                v => v.NormalizedName.Contains(upper!) ||
                     (v.Category != null && v.Category.ToUpper().Contains(upper!)))
            .OrderBy(v => v.Name)
            .Select(v => new VendorProjection(
                v,
                v.DefaultCategory != null ? v.DefaultCategory.Name : null,
                Context.Transactions.Count(t => t.VendorId == v.Id),
                Context.Transactions.Where(t => t.VendorId == v.Id).Sum(t => (decimal?)t.DebitAmount) ?? 0m));

        return await query.ToPagedResultAsync(pageNumber, pageSize, cancellationToken);
    }
}

public sealed class UploadHistoryRepository(ApplicationDbContext context)
    : GenericRepository<UploadHistory>(context), IUploadHistoryRepository
{
    public async Task<PagedResult<UploadHistory>> SearchAsync(
        UploadHistoryFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = Set.AsNoTracking()
            .WhereIf(filter.From.HasValue, h => h.StartedOnUtc >= filter.From!.Value)
            .WhereIf(filter.To.HasValue, h => h.StartedOnUtc <= filter.To!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword), h => h.FileName.ToUpper().Contains(keyword!));

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.Select(s => (UploadStatus)s).ToList();
            query = query.Where(h => statuses.Contains(h.Status));
        }

        query = query.ApplySort(filter.SortBy, filter.SortDirection, nameof(UploadHistory.StartedOnUtc));

        return await query.ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<UploadHistory>> GetPendingAsync(int maxCount, CancellationToken cancellationToken = default) =>
        await Set.Where(h => h.Status == UploadStatus.Pending || h.Status == UploadStatus.Processing)
            .OrderBy(h => h.StartedOnUtc)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
}

/// <summary>
/// Read side of the uploaded-file lifecycle. The file page is fetched first and its derived facts are then
/// resolved in a few set-based queries, rather than one correlated subquery per column per row.
/// Every query calls <c>IgnoreQueryFilters</c> and states the deleted state explicitly, because the recycle
/// bin needs to read rows the global soft-delete filter would otherwise hide.
/// </summary>
public sealed class StatementFileRepository(ApplicationDbContext context)
    : GenericRepository<StatementFile>(context), IStatementFileRepository
{
    public async Task<PagedResult<StatementFileProjection>> SearchAsync(
        StatementFileFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var deleted = filter.DeletedOnly;
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = Context.StatementFiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => f.IsDeleted == deleted)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword), f => f.OriginalFileName.ToUpper().Contains(keyword!));

        // Bank and period live on the statement, so those filters become an EXISTS over matching statements.
        if (filter.BankIds is { Count: > 0 } || filter.PeriodFrom.HasValue || filter.PeriodTo.HasValue)
        {
            var matching = Context.Statements
                .IgnoreQueryFilters()
                .Where(s => s.IsDeleted == deleted && s.StatementFileId != null)
                .WhereIf(filter.BankIds is { Count: > 0 }, s => filter.BankIds!.Contains(s.BankId))
                .WhereIf(filter.PeriodFrom.HasValue, s => s.PeriodEnd >= filter.PeriodFrom!.Value)
                .WhereIf(filter.PeriodTo.HasValue, s => s.PeriodStart <= filter.PeriodTo!.Value)
                .Select(s => s.StatementFileId!.Value);

            query = query.Where(f => matching.Contains(f.Id));
        }

        var sortFallback = deleted ? nameof(StatementFile.DeletedOnUtc) : nameof(StatementFile.CreatedOnUtc);
        var page = await query
            .ApplySort(filter.SortBy, filter.SortDirection, sortFallback)
            .ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);

        var items = await ProjectAsync(page.Items, deleted, cancellationToken);

        return new PagedResult<StatementFileProjection>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    public async Task<StatementFileProjection?> GetProjectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var file = await Context.StatementFiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (file is null)
        {
            return null;
        }

        var projected = await ProjectAsync([file], file.IsDeleted, cancellationToken);
        return projected[0];
    }

    public async Task<IReadOnlyList<StatementDto>> GetStatementsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var statements = await Context.Statements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.Bank)
            .Include(s => s.StatementFile)
            .Where(s => s.StatementFileId == id)
            .OrderBy(s => s.PeriodStart)
            .ToListAsync(cancellationToken);

        return statements
            .Select(s => s.ToDto(s.Bank?.Name ?? "Unknown", s.StatementFile?.OriginalFileName))
            .ToList();
    }

    public async Task<IReadOnlyList<UploadHistoryDto>> GetUploadsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var uploads = await Context.UploadHistories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.StatementFileId == id)
            .OrderByDescending(u => u.StartedOnUtc)
            .ToListAsync(cancellationToken);

        return uploads.Select(u => u.ToDto()).ToList();
    }

    public async Task<PagedResult<TransactionDto>> GetTransactionsAsync(
        Guid id,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var statementIds = await StatementIdsFor(id).ToListAsync(cancellationToken);
        if (statementIds.Count == 0)
        {
            return PagedResult<TransactionDto>.Empty(pagination.PageNumber, pagination.PageSize);
        }

        return await Context.Transactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.StatementId != null && statementIds.Contains(t.StatementId.Value))
            .ApplySort(pagination.SortBy, pagination.SortDirection, nameof(Transaction.TransactionDate))
            .Select(ProjectionMappings.TransactionProjection)
            .ToPagedResultAsync(pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    /// <summary>
    /// Only files that produced a live statement are offered: a failed or password-locked upload has no
    /// transactions, so filtering by it could never narrow anything.
    /// </summary>
    public async Task<IReadOnlyList<StatementFileLookupDto>> GetLookupAsync(
        Guid? bankId,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var statements = await Context.Statements
            .AsNoTracking()
            .Where(s => s.StatementFileId != null && s.StatementFile!.IsDeleted == false)
            .WhereIf(bankId.HasValue, s => s.BankId == bankId!.Value)
            .WhereIf(year.HasValue, s => s.PeriodEnd.Year == year!.Value)
            .Select(s => new
            {
                FileId = s.StatementFileId!.Value,
                FileName = s.StatementFile!.OriginalFileName,
                UploadedOnUtc = s.StatementFile!.CreatedOnUtc,
                s.BankId,
                BankName = s.Bank!.Name,
                s.PeriodStart,
                s.PeriodEnd,
                s.TransactionCount
            })
            .ToListAsync(cancellationToken);

        return statements
            .GroupBy(s => s.FileId)
            .Select(group =>
            {
                var first = group.First();
                var periodStart = group.Min(s => s.PeriodStart);
                var periodEnd = group.Max(s => s.PeriodEnd);

                return new StatementFileLookupDto(
                    group.Key,
                    StatementFileLabel.Build(first.BankName, first.FileName, periodStart, periodEnd, first.UploadedOnUtc),
                    first.FileName,
                    first.BankId,
                    first.BankName,
                    periodStart,
                    periodEnd,
                    group.Sum(s => s.TransactionCount));
            })
            .OrderByDescending(f => f.PeriodEnd)
            .ThenBy(f => f.OriginalFileName)
            .ToList();
    }

    private IQueryable<Guid> StatementIdsFor(Guid fileId) =>
        Context.Statements
            .IgnoreQueryFilters()
            .Where(s => s.StatementFileId == fileId)
            .Select(s => s.Id);

    /// <summary>
    /// Resolves the derived facts for one page of files. Children are matched on the same deleted state as
    /// their file, because a soft delete cascades the whole chain in a single transaction.
    /// </summary>
    private async Task<IReadOnlyList<StatementFileProjection>> ProjectAsync(
        IReadOnlyList<StatementFile> files,
        bool deleted,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var fileIds = files.Select(f => f.Id).ToList();

        var statements = await Context.Statements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.StatementFileId != null && fileIds.Contains(s.StatementFileId.Value) && s.IsDeleted == deleted)
            .Select(s => new
            {
                FileId = s.StatementFileId!.Value,
                s.Id,
                s.BankId,
                BankName = s.Bank!.Name,
                s.PeriodStart,
                s.PeriodEnd,
                s.StatementDate
            })
            .ToListAsync(cancellationToken);

        var statementIds = statements.Select(s => s.Id).ToList();

        var transactionCounts = await Context.Transactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.StatementId != null && statementIds.Contains(t.StatementId.Value) && t.IsDeleted == deleted)
            .GroupBy(t => t.StatementId!.Value)
            .Select(g => new { StatementId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countByStatement = transactionCounts.ToDictionary(c => c.StatementId, c => c.Count);

        var uploads = await Context.UploadHistories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.StatementFileId != null && fileIds.Contains(u.StatementFileId.Value) && u.IsDeleted == deleted)
            .Select(u => new { FileId = u.StatementFileId!.Value, u.Status, u.StartedOnUtc, u.CreatedBy })
            .ToListAsync(cancellationToken);

        var statementsByFile = statements.ToLookup(s => s.FileId);
        var uploadsByFile = uploads.ToLookup(u => u.FileId);

        return files.Select(file =>
        {
            var fileStatements = statementsByFile[file.Id].ToList();
            var fileUploads = uploadsByFile[file.Id].OrderByDescending(u => u.StartedOnUtc).ToList();
            var latestUpload = fileUploads.FirstOrDefault();

            var bankName = fileStatements.FirstOrDefault()?.BankName
                           ?? (file.DetectedBank == BankCode.Unknown ? null : file.DetectedBank.ToString());

            DateTime? periodStart = fileStatements.Count == 0 ? null : fileStatements.Min(s => s.PeriodStart);
            DateTime? periodEnd = fileStatements.Count == 0 ? null : fileStatements.Max(s => s.PeriodEnd);

            return new StatementFileProjection(
                file,
                fileStatements.FirstOrDefault()?.BankId,
                bankName,
                periodStart,
                periodEnd,
                fileStatements.Count == 0 ? null : fileStatements.Max(s => s.StatementDate),
                fileStatements.Count,
                fileStatements.Sum(s => countByStatement.GetValueOrDefault(s.Id)),
                fileUploads.Count,
                latestUpload?.Status.ToString(),
                latestUpload?.StartedOnUtc ?? file.CreatedOnUtc,
                latestUpload?.CreatedBy ?? file.CreatedBy);
        }).ToList();
    }
}

public sealed class AuditLogRepository(ApplicationDbContext context) : IAuditLogRepository
{    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await context.AuditLogs.AddAsync(log, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<Contracts.Reports.AuditLogDto>> SearchAsync(
        Contracts.Reports.AuditLogFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = context.AuditLogs
            .AsNoTracking()
            .WhereIf(filter.From.HasValue, a => a.TimestampUtc >= filter.From!.Value)
            .WhereIf(filter.To.HasValue, a => a.TimestampUtc <= filter.To!.Value)
            .WhereIf(filter.UserId.HasValue, a => a.UserId == filter.UserId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(filter.EntityName), a => a.EntityName == filter.EntityName)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword),
                a => (a.UserName != null && a.UserName.ToUpper().Contains(keyword!)) ||
                     (a.Endpoint != null && a.Endpoint.ToUpper().Contains(keyword!)) ||
                     (a.Message != null && a.Message.ToUpper().Contains(keyword!)));

        if (filter.Actions is { Count: > 0 })
        {
            var actions = filter.Actions.Select(a => (AuditAction)a).ToList();
            query = query.Where(a => actions.Contains(a.Action));
        }

        var projected = query
            .OrderByDescending(a => a.TimestampUtc)
            .Select(a => new Contracts.Reports.AuditLogDto(
                a.Id,
                a.Action.ToString(),
                a.EntityName,
                a.EntityId,
                a.UserId,
                a.UserName,
                a.TimestampUtc,
                a.IpAddress,
                a.Endpoint,
                a.HttpMethod,
                a.StatusCode,
                a.DurationMs,
                a.Message,
                a.AffectedColumns));

        return await projected.ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);
    }
}
