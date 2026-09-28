using System.Linq.Expressions;
using FinanceAudit360.Application.Common.Extensions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Application.Common.Specifications;
using FinanceAudit360.Contracts.Dashboard;
using FinanceAudit360.Contracts.Reports;
using FinanceAudit360.Contracts.Transactions;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Persistence.Repositories;

public sealed class TransactionRepository(ApplicationDbContext context)
    : GenericRepository<Transaction>(context), ITransactionRepository
{
    private static readonly string[] SortableColumns =
    [
        nameof(Transaction.TransactionDate),
        nameof(Transaction.PostingDate),
        nameof(Transaction.Amount),
        nameof(Transaction.CreditAmount),
        nameof(Transaction.DebitAmount),
        nameof(Transaction.SignedAmount),
        nameof(Transaction.Description),
        nameof(Transaction.ReferenceNumber),
        nameof(Transaction.TransactionType),
        nameof(Transaction.CreatedOnUtc)
    ];

    public async Task<TransactionSearchResultDto> SearchAsync(
        TransactionFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().Where(BuildPredicate(filter));

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Credit = g.Sum(t => t.CreditAmount),
                Debit = g.Sum(t => t.DebitAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalCount = totals?.Count ?? 0;
        var totalCredit = totals?.Credit ?? 0m;
        var totalDebit = totals?.Debit ?? 0m;

        var sortBy = SortableColumns.Contains(filter.SortBy, StringComparer.OrdinalIgnoreCase)
            ? filter.SortBy
            : nameof(Transaction.TransactionDate);

        var items = await query
            .ApplySort(sortBy, filter.SortDirection, nameof(Transaction.TransactionDate))
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(ProjectionMappings.TransactionProjection)
            .ToListAsync(cancellationToken);

        var groups = await BuildGroupsAsync(query, filter.GroupBy, cancellationToken);

        var totalPages = filter.PageSize == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        return new TransactionSearchResultDto(
            items,
            totalCount,
            filter.PageNumber,
            filter.PageSize,
            totalPages,
            totalCredit,
            totalDebit,
            totalCredit - totalDebit,
            groups);
    }

    /// <summary>
    /// Assembles the explorer predicate. Each list filter becomes an IN (...) clause (OR within the
    /// filter) and every filter is ANDed together; the keyword spans several columns as one OR group.
    /// </summary>
    private static Expression<Func<Transaction, bool>> BuildPredicate(TransactionFilterRequest filter)
    {
        var builder = PredicateBuilder<Transaction>.Create();

        builder.AndIf(filter.DateFrom.HasValue, t => t.TransactionDate >= filter.DateFrom!.Value);
        builder.AndIf(filter.DateTo.HasValue, t => t.TransactionDate <= filter.DateTo!.Value);
        builder.AndIf(filter.PostingDateFrom.HasValue, t => t.PostingDate >= filter.PostingDateFrom!.Value);
        builder.AndIf(filter.PostingDateTo.HasValue, t => t.PostingDate <= filter.PostingDateTo!.Value);
        builder.AndIf(filter.AmountFrom.HasValue, t => t.Amount >= filter.AmountFrom!.Value);
        builder.AndIf(filter.AmountTo.HasValue, t => t.Amount <= filter.AmountTo!.Value);

        builder.AndIf(filter.CreditOnly == true, t => t.Direction == TransactionDirection.Credit);
        builder.AndIf(filter.DebitOnly == true, t => t.Direction == TransactionDirection.Debit);

        builder.AndIf(HasValues(filter.BankIds), t => t.BankId.HasValue && filter.BankIds!.Contains(t.BankId.Value));
        builder.AndIf(HasValues(filter.CreditCardIds), t => t.CreditCardId.HasValue && filter.CreditCardIds!.Contains(t.CreditCardId.Value));
        builder.AndIf(HasValues(filter.BankAccountIds), t => t.BankAccountId.HasValue && filter.BankAccountIds!.Contains(t.BankAccountId.Value));
        builder.AndIf(HasValues(filter.VendorIds), t => t.VendorId.HasValue && filter.VendorIds!.Contains(t.VendorId.Value));
        builder.AndIf(HasValues(filter.PersonIds), t => t.PersonId.HasValue && filter.PersonIds!.Contains(t.PersonId.Value));
        builder.AndIf(HasValues(filter.CategoryIds), t => t.CategoryId.HasValue && filter.CategoryIds!.Contains(t.CategoryId.Value));
        builder.AndIf(HasValues(filter.StatementIds), t => t.StatementId.HasValue && filter.StatementIds!.Contains(t.StatementId.Value));

        // A file owns its transactions through its statement, so filtering by source PDF walks that link.
        builder.AndIf(
            HasValues(filter.StatementFileIds),
            t => t.Statement != null &&
                 t.Statement.StatementFileId.HasValue &&
                 filter.StatementFileIds!.Contains(t.Statement.StatementFileId.Value));

        if (filter.TransactionTypes is { Count: > 0 })
        {
            var types = filter.TransactionTypes.Select(v => (TransactionType)v).ToList();
            builder.And(t => types.Contains(t.TransactionType));
        }

        builder.AndIf(filter.IsReconciled.HasValue, t => t.IsReconciled == filter.IsReconciled!.Value);
        builder.AndIf(filter.IsDisputed.HasValue, t => t.IsDisputed == filter.IsDisputed!.Value);
        builder.AndIf(filter.IsRecurring.HasValue, t => t.IsRecurring == filter.IsRecurring!.Value);

        if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
        {
            var reference = filter.ReferenceNumber.Trim();
            builder.And(t => t.ReferenceNumber != null && t.ReferenceNumber.Contains(reference));
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim().ToUpperInvariant();
            builder.AndAnyOf(
                t => t.NormalizedDescription.Contains(keyword),
                t => t.ReferenceNumber != null && t.ReferenceNumber.ToUpper().Contains(keyword),
                t => t.MerchantRawText != null && t.MerchantRawText.ToUpper().Contains(keyword),
                t => t.Notes != null && t.Notes.ToUpper().Contains(keyword),
                t => t.Vendor != null && t.Vendor.NormalizedName.Contains(keyword),
                t => t.Person != null && t.Person.NormalizedName.Contains(keyword));
        }

        return builder.BuildOrTrue();
    }

    private static bool HasValues(IReadOnlyList<Guid>? values) => values is { Count: > 0 };

    private async Task<IReadOnlyList<TransactionGroupDto>> BuildGroupsAsync(
        IQueryable<Transaction> query,
        string? groupBy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return [];
        }

        return groupBy.Trim().ToLowerInvariant() switch
        {
            "vendor" => await GroupByForeignKeyAsync(query, t => t.VendorId, VendorLabelsAsync, "(Unassigned)", cancellationToken),
            "person" => await GroupByForeignKeyAsync(query, t => t.PersonId, PersonLabelsAsync, "(Unassigned)", cancellationToken),
            "card" => await GroupByForeignKeyAsync(query, t => t.CreditCardId, CardLabelsAsync, "(No card)", cancellationToken),
            "account" => await GroupByForeignKeyAsync(query, t => t.BankAccountId, AccountLabelsAsync, "(No account)", cancellationToken),
            "bank" => await GroupByForeignKeyAsync(query, t => t.BankId, BankLabelsAsync, "(No bank)", cancellationToken),
            "category" => await GroupByForeignKeyAsync(query, t => t.CategoryId, CategoryLabelsAsync, "(Uncategorised)", cancellationToken),
            "type" => await GroupByTypeAsync(query, cancellationToken),
            "month" => await GroupByMonthAsync(query, cancellationToken),
            _ => []
        };
    }

    /// <summary>
    /// Aggregates in SQL over a scalar foreign key only. Grouping by a navigation property or an
    /// owned value object is not translatable, so display labels are resolved in a second query.
    /// </summary>
    private async Task<IReadOnlyList<TransactionGroupDto>> GroupByForeignKeyAsync(
        IQueryable<Transaction> query,
        Expression<Func<Transaction, Guid?>> keySelector,
        Func<IReadOnlyList<Guid>, CancellationToken, Task<IReadOnlyDictionary<Guid, string>>> labelResolver,
        string unassignedLabel,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(keySelector)
            .Select(g => new
            {
                Key = g.Key,
                Count = g.Count(),
                Credit = g.Sum(t => t.CreditAmount),
                Debit = g.Sum(t => t.DebitAmount)
            })
            .OrderByDescending(g => g.Debit)
            .Take(100)
            .ToListAsync(cancellationToken);

        var ids = rows.Where(r => r.Key.HasValue).Select(r => r.Key!.Value).ToList();
        var labels = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await labelResolver(ids, cancellationToken);

        return rows
            .Select(r => new TransactionGroupDto(
                r.Key?.ToString() ?? "none",
                r.Key.HasValue ? labels.GetValueOrDefault(r.Key.Value, unassignedLabel) : unassignedLabel,
                r.Count,
                r.Credit,
                r.Debit,
                r.Credit - r.Debit))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, string>> VendorLabelsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await Context.Vendors.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.DisplayName ?? v.Name, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> PersonLabelsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await Context.Persons.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> CardLabelsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await Context.CreditCards.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nickname ?? "Card ****" + c.CardNumber.Last4, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> AccountLabelsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await Context.BankAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Nickname ?? "A/c ****" + a.AccountNumberLast4, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> BankLabelsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await Context.Banks.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> CategoryLabelsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await Context.Categories.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

    private static async Task<IReadOnlyList<TransactionGroupDto>> GroupByTypeAsync(
        IQueryable<Transaction> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(t => t.TransactionType)
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                Credit = g.Sum(t => t.CreditAmount),
                Debit = g.Sum(t => t.DebitAmount)
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(r => r.Debit)
            .Select(r => new TransactionGroupDto(
                r.Type.ToString(),
                r.Type.ToString(),
                r.Count,
                r.Credit,
                r.Debit,
                r.Credit - r.Debit))
            .ToList();
    }

    private static async Task<IReadOnlyList<TransactionGroupDto>> GroupByMonthAsync(
        IQueryable<Transaction> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Count = g.Count(),
                Credit = g.Sum(t => t.CreditAmount),
                Debit = g.Sum(t => t.DebitAmount)
            })
            .OrderByDescending(g => g.Year)
            .ThenByDescending(g => g.Month)
            .Take(120)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TransactionGroupDto(
                $"{r.Year:D4}-{r.Month:D2}",
                $"{new DateTime(r.Year, r.Month, 1):MMM yyyy}",
                r.Count,
                r.Credit,
                r.Debit,
                r.Credit - r.Debit))
            .ToList();
    }

    public Task<Transaction?> GetWithRelationsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .Include(t => t.Vendor)
            .Include(t => t.Category)
            .Include(t => t.Bank)
            .Include(t => t.BankAccount)
            .Include(t => t.CreditCard)
            .Include(t => t.Person)
            .Include(t => t.Statement)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
        await Set.Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetExistingDedupeHashesAsync(
        IReadOnlyList<string> hashes,
        CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(t => hashes.Contains(t.DedupeHash))
            .Select(t => t.DedupeHash)
            .ToListAsync(cancellationToken);

    public async Task<DashboardTotals> GetTotalsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var window = Set.AsNoTracking().Where(t => t.TransactionDate >= from && t.TransactionDate <= to);

        var aggregate = await window
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Credit = g.Sum(t => t.CreditAmount),
                Debit = g.Sum(t => t.DebitAmount),
                Count = g.Count(),
                Vendors = g.Select(t => t.VendorId).Distinct().Count(),
                Persons = g.Select(t => t.PersonId).Distinct().Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return aggregate is null
            ? new DashboardTotals(0m, 0m, 0, 0, 0)
            : new DashboardTotals(aggregate.Credit, aggregate.Debit, aggregate.Count, aggregate.Vendors, aggregate.Persons);
    }

    /// <summary>
    /// Grouped in SQL by the card id alone - an owned value object such as the masked card number
    /// cannot appear in a GROUP BY key - and the display name is attached afterwards.
    /// </summary>
    public async Task<IReadOnlyList<TopEntityDto>> GetTopSpendingCardsAsync(
        DateTime from,
        DateTime to,
        int topN,
        CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking()
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to && t.CreditCardId != null && t.DebitAmount > 0)
            .GroupBy(t => t.CreditCardId!.Value)
            .Select(g => new { CardId = g.Key, Amount = g.Sum(t => t.DebitAmount), Count = g.Count() })
            .OrderByDescending(x => x.Amount)
            .Take(topN)
            .ToListAsync(cancellationToken);

        return await AttachCardNamesAsync(rows.Select(r => (r.CardId, r.Amount, r.Count)).ToList(), cancellationToken);
    }

    private async Task<IReadOnlyList<TopEntityDto>> AttachCardNamesAsync(
        IReadOnlyList<(Guid CardId, decimal Amount, int Count)> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.CardId).ToList();
        var names = await Context.CreditCards
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, Name = c.Nickname ?? "Card ****" + c.CardNumber.Last4 })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return rows
            .Select(r => new TopEntityDto(r.CardId, names.GetValueOrDefault(r.CardId, "Unknown card"), r.Amount, r.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<TopEntityDto>> GetTopSpendingVendorsAsync(
        DateTime from,
        DateTime to,
        int topN,
        CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking()
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to && t.VendorId != null && t.DebitAmount > 0)
            .GroupBy(t => t.VendorId!.Value)
            .Select(g => new { Id = g.Key, Amount = g.Sum(t => t.DebitAmount), Count = g.Count() })
            .OrderByDescending(x => x.Amount)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var labels = await VendorLabelsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows
            .Select(r => new TopEntityDto(r.Id, labels.GetValueOrDefault(r.Id, "Unknown vendor"), r.Amount, r.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<TopEntityDto>> GetMostActiveBanksAsync(
        DateTime from,
        DateTime to,
        int topN,
        CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking()
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to && t.BankId != null)
            .GroupBy(t => t.BankId!.Value)
            .Select(g => new { Id = g.Key, Amount = g.Sum(t => t.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var labels = await BankLabelsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows
            .Select(r => new TopEntityDto(r.Id, labels.GetValueOrDefault(r.Id, "Unknown bank"), r.Amount, r.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyActivityDto>> GetMonthlyActivityAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking()
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Credit = g.Sum(t => t.CreditAmount),
                Debit = g.Sum(t => t.DebitAmount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.Year)
            .ThenBy(r => r.Month)
            .Select(r => new MonthlyActivityDto(
                r.Year,
                r.Month,
                new DateTime(r.Year, r.Month, 1).ToString("MMM yyyy"),
                r.Credit,
                r.Debit,
                r.Credit - r.Debit,
                r.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<SpendByEntityDto>> GetSpendByVendorAsync(
        ReportFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var rows = await ApplyReportFilter(filter)
            .Where(t => t.VendorId != null)
            .GroupBy(t => t.VendorId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Debit = g.Sum(t => t.DebitAmount),
                Credit = g.Sum(t => t.CreditAmount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Debit)
            .Take(filter.TopN)
            .ToListAsync(cancellationToken);

        var labels = await VendorLabelsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows
            .Select(r => new SpendByEntityDto(
                r.Id,
                labels.GetValueOrDefault(r.Id, "Unknown vendor"),
                r.Debit,
                r.Credit,
                r.Credit - r.Debit,
                r.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<SpendByEntityDto>> GetSpendByCategoryAsync(
        ReportFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var rows = await ApplyReportFilter(filter)
            .Where(t => t.CategoryId != null)
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Debit = g.Sum(t => t.DebitAmount),
                Credit = g.Sum(t => t.CreditAmount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Debit)
            .Take(filter.TopN)
            .ToListAsync(cancellationToken);

        var labels = await CategoryLabelsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows
            .Select(r => new SpendByEntityDto(
                r.Id,
                labels.GetValueOrDefault(r.Id, "Uncategorised"),
                r.Debit,
                r.Credit,
                r.Credit - r.Debit,
                r.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<CardPaymentSummaryDto>> GetCardPaymentSummaryAsync(
        ReportFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var rows = await ApplyReportFilter(filter)
            .Where(t => t.CreditCardId != null)
            .GroupBy(t => t.CreditCardId!.Value)
            .Select(g => new
            {
                CreditCardId = g.Key,
                TotalSpend = g.Sum(t => t.DebitAmount),
                TotalPayments = g.Sum(t => t.CreditAmount),
                Count = g.Count(),
                LastPaymentOn = g.Where(t => t.TransactionType == TransactionType.Payment)
                    .Max(t => (DateTime?)t.TransactionDate)
            })
            .OrderByDescending(x => x.TotalSpend)
            .Take(filter.TopN)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.CreditCardId).ToList();
        var cards = await Context.CreditCards
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                CardName = c.Nickname ?? "Card ****" + c.CardNumber.Last4,
                BankName = c.Bank!.Name,
                c.CurrentOutstanding
            })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return rows
            .Select(r =>
            {
                var card = cards.GetValueOrDefault(r.CreditCardId);
                return new CardPaymentSummaryDto(
                    r.CreditCardId,
                    card?.CardName ?? "Unknown card",
                    card?.BankName ?? "Unknown bank",
                    r.TotalSpend,
                    r.TotalPayments,
                    card?.CurrentOutstanding ?? 0m,
                    r.Count,
                    r.LastPaymentOn);
            })
            .ToList();
    }

    private IQueryable<Transaction> ApplyReportFilter(ReportFilterRequest filter)
    {
        var query = Set.AsNoTracking();

        if (filter.From.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= filter.To.Value);
        }

        if (filter.BankIds is { Count: > 0 })
        {
            query = query.Where(t => t.BankId.HasValue && filter.BankIds.Contains(t.BankId.Value));
        }

        if (filter.CreditCardIds is { Count: > 0 })
        {
            query = query.Where(t => t.CreditCardId.HasValue && filter.CreditCardIds.Contains(t.CreditCardId.Value));
        }

        if (filter.BankAccountIds is { Count: > 0 })
        {
            query = query.Where(t => t.BankAccountId.HasValue && filter.BankAccountIds.Contains(t.BankAccountId.Value));
        }

        return query;
    }

    public async Task<int> BulkAssignAsync(
        BulkAssignRequest request,
        string? modifiedBy,
        CancellationToken cancellationToken = default)
    {
        var transactions = await Set
            .Where(t => request.TransactionIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            if (request.VendorId.HasValue)
            {
                transaction.AssignVendor(request.VendorId);
            }

            if (request.CategoryId.HasValue)
            {
                transaction.AssignCategory(request.CategoryId);
            }

            if (request.PersonId.HasValue)
            {
                transaction.AssignPerson(request.PersonId);
            }

            transaction.SetModified(modifiedBy, DateTime.UtcNow);
        }

        await Context.SaveChangesAsync(cancellationToken);
        return transactions.Count;
    }
}
