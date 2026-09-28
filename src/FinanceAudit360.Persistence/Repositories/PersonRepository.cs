using FinanceAudit360.Application.Common.Extensions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Dashboard;
using FinanceAudit360.Contracts.Persons;
using FinanceAudit360.Contracts.Reports;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Persistence.Repositories;

public sealed class PersonRepository(ApplicationDbContext context)
    : GenericRepository<Person>(context), IPersonRepository
{
    public Task<Person?> GetWithLedgerAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.Include(p => p.LedgerEntries).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PagedResult<PersonDto>> SearchAsync(PersonFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = Set.AsNoTracking()
            .WhereIf(filter.IsActive.HasValue, p => p.IsActive == filter.IsActive!.Value)
            .WhereIf(filter.HasOutstanding == true, p => p.OutstandingBalance != 0m)
            .WhereIf(filter.MinOutstanding.HasValue, p => p.OutstandingBalance >= filter.MinOutstanding!.Value)
            .WhereIf(filter.MaxOutstanding.HasValue, p => p.OutstandingBalance <= filter.MaxOutstanding!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword),
                p => p.NormalizedName.Contains(keyword!) ||
                     (p.Mobile != null && p.Mobile.Contains(keyword!)) ||
                     (p.Email != null && p.Email.ToUpper().Contains(keyword!)) ||
                     (p.Relationship != null && p.Relationship.ToUpper().Contains(keyword!)))
            .ApplySort(filter.SortBy, filter.SortDirection, nameof(Person.Name))
            .Select(ProjectionMappings.PersonProjection);

        return await query.ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);
    }

    public async Task<PagedResult<LedgerEntryDto>> GetLedgerAsync(
        Guid personId,
        PersonLedgerFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = Context.MoneyLedgers
            .AsNoTracking()
            .Where(l => l.PersonId == personId)
            .WhereIf(filter.DateFrom.HasValue, l => l.EntryDate >= filter.DateFrom!.Value)
            .WhereIf(filter.DateTo.HasValue, l => l.EntryDate <= filter.DateTo!.Value)
            .WhereIf(filter.AmountFrom.HasValue, l => l.Amount >= filter.AmountFrom!.Value)
            .WhereIf(filter.AmountTo.HasValue, l => l.Amount <= filter.AmountTo!.Value)
            .WhereIf(filter.BankAccountId.HasValue, l => l.BankAccountId == filter.BankAccountId)
            .WhereIf(filter.CreditCardId.HasValue, l => l.CreditCardId == filter.CreditCardId)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword),
                l => (l.Description != null && l.Description.ToUpper().Contains(keyword!)) ||
                     (l.ReferenceNumber != null && l.ReferenceNumber.ToUpper().Contains(keyword!)));

        if (filter.EntryTypes is { Count: > 0 })
        {
            var types = filter.EntryTypes.Select(v => (LedgerEntryType)v).ToList();
            query = query.Where(l => types.Contains(l.EntryType));
        }

        var projected = query
            .ApplySort(filter.SortBy, filter.SortDirection, nameof(MoneyLedger.EntryDate))
            .Select(ProjectionMappings.LedgerProjection);

        return await projected.ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);
    }

    public async Task<PersonAuditDto?> GetAuditAsync(
        Guid personId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var person = await Set.AsNoTracking()
            .Where(p => p.Id == personId)
            .Select(ProjectionMappings.PersonProjection)
            .FirstOrDefaultAsync(cancellationToken);

        if (person is null)
        {
            return null;
        }

        var ledgerQuery = Context.MoneyLedgers
            .AsNoTracking()
            .Where(l => l.PersonId == personId)
            .WhereIf(from.HasValue, l => l.EntryDate >= from!.Value)
            .WhereIf(to.HasValue, l => l.EntryDate <= to!.Value);

        var totals = await ledgerQuery
            .GroupBy(l => l.EntryType)
            .Select(g => new { EntryType = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        decimal TotalFor(LedgerEntryType type) => totals.FirstOrDefault(t => t.EntryType == type)?.Total ?? 0m;

        var given = TotalFor(LedgerEntryType.Given);
        var taken = TotalFor(LedgerEntryType.Taken);
        var settlementsReceived = TotalFor(LedgerEntryType.SettlementReceived);
        var settlementsPaid = TotalFor(LedgerEntryType.SettlementPaid);

        var recent = await ledgerQuery
            .OrderByDescending(l => l.EntryDate)
            .Take(25)
            .Select(ProjectionMappings.LedgerProjection)
            .ToListAsync(cancellationToken);

        var monthly = await ledgerQuery
            .GroupBy(l => new { l.EntryDate.Year, l.EntryDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Given = g.Sum(l => l.EntryType == LedgerEntryType.Given ? l.Amount : 0m),
                Taken = g.Sum(l => l.EntryType == LedgerEntryType.Taken ? l.Amount : 0m),
                SettledIn = g.Sum(l => l.EntryType == LedgerEntryType.SettlementReceived ? l.Amount : 0m),
                SettledOut = g.Sum(l => l.EntryType == LedgerEntryType.SettlementPaid ? l.Amount : 0m)
            })
            .ToListAsync(cancellationToken);

        var timeline = new List<PersonTimelinePointDto>();
        var running = 0m;

        foreach (var row in monthly.OrderBy(r => r.Year).ThenBy(r => r.Month))
        {
            running += row.Given - row.SettledIn - (row.Taken - row.SettledOut);
            timeline.Add(new PersonTimelinePointDto(
                new DateTime(row.Year, row.Month, 1).ToString("MMM yyyy"),
                row.Given,
                row.Taken,
                running));
        }

        var transactionCount = await Context.Transactions.CountAsync(t => t.PersonId == personId, cancellationToken);

        return new PersonAuditDto(
            person,
            given,
            taken,
            given - settlementsReceived - (taken - settlementsPaid),
            settlementsReceived,
            settlementsPaid,
            transactionCount,
            recent,
            timeline);
    }

    /// <summary>Ordered by magnitude so the largest receivable or payable appears first.</summary>
    public async Task<IReadOnlyList<PersonOutstandingDto>> GetOutstandingAsync(int topN, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(p => p.OutstandingBalance != 0m)
            .OrderByDescending(p => p.OutstandingBalance > 0m ? p.OutstandingBalance : -p.OutstandingBalance)
            .Take(topN)
            .Select(p => new PersonOutstandingDto(p.Id, p.Name, p.TotalGiven, p.TotalTaken, p.OutstandingBalance, p.LastActivityOn))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Grouping happens on the scalar person id because a navigation property cannot appear in a
    /// translatable GROUP BY key; the name is looked up afterwards.
    /// </summary>
    public async Task<TopEntityDto?> GetMostActivePersonAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var fromLedger = await Context.MoneyLedgers
            .AsNoTracking()
            .Where(l => l.EntryDate >= from && l.EntryDate <= to)
            .GroupBy(l => l.PersonId)
            .Select(g => new { Id = g.Key, Amount = g.Sum(l => l.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefaultAsync(cancellationToken);

        if (fromLedger is not null)
        {
            var name = await Set.AsNoTracking()
                .Where(p => p.Id == fromLedger.Id)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);

            return new TopEntityDto(fromLedger.Id, name ?? "Unknown person", fromLedger.Amount, fromLedger.Count);
        }

        var fromTransactions = await Context.Transactions
            .AsNoTracking()
            .Where(t => t.PersonId != null && t.TransactionDate >= from && t.TransactionDate <= to)
            .GroupBy(t => t.PersonId!.Value)
            .Select(g => new { Id = g.Key, Amount = g.Sum(t => t.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefaultAsync(cancellationToken);

        if (fromTransactions is null)
        {
            return null;
        }

        var personName = await Set.AsNoTracking()
            .Where(p => p.Id == fromTransactions.Id)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return new TopEntityDto(fromTransactions.Id, personName ?? "Unknown person", fromTransactions.Amount, fromTransactions.Count);
    }

    /// <summary>
    /// Finds the person whose configured keywords appear in a statement narration.
    /// Matching is done in memory over the (small) keyword list because SQL cannot express it.
    /// </summary>
    public async Task<Person?> MatchByKeywordAsync(string narration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(narration))
        {
            return null;
        }

        var upper = narration.ToUpperInvariant();

        var candidates = await Set.AsNoTracking()
            .Where(p => p.IsActive && p.MatchKeywords != null && p.MatchKeywords != "")
            .Select(p => new { p.Id, p.MatchKeywords })
            .ToListAsync(cancellationToken);

        var matchId = candidates
            .Where(c => c.MatchKeywords!
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(token => upper.Contains(token.ToUpperInvariant(), StringComparison.Ordinal)))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefault();

        return matchId is null ? null : await Set.FirstOrDefaultAsync(p => p.Id == matchId, cancellationToken);
    }
}
