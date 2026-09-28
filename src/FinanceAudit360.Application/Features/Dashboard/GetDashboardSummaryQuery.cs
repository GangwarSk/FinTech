using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Contracts.Dashboard;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Extensions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Dashboard;

public sealed record GetDashboardSummaryQuery(DashboardPeriod Period, DateTime? From, DateTime? To)
    : IRequest<DashboardSummaryDto>;

public sealed class GetDashboardSummaryQueryValidator : AbstractValidator<GetDashboardSummaryQuery>
{
    public GetDashboardSummaryQueryValidator()
    {
        RuleFor(x => x.From)
            .NotNull()
            .When(x => x.Period == DashboardPeriod.Custom)
            .WithMessage("A start date is required for a custom period.");

        RuleFor(x => x.To)
            .NotNull()
            .When(x => x.Period == DashboardPeriod.Custom)
            .WithMessage("An end date is required for a custom period.");

        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithName("DateRange")
            .WithMessage("The start date must not be after the end date.");
    }
}

public sealed class GetDashboardSummaryQueryHandler(
    IApplicationDbContext context,
    ITransactionRepository transactionRepository,
    IPersonRepository personRepository,
    IDateTimeProvider dateTime) : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(request, dateTime.UtcNow);
        var (previousFrom, previousTo) = DateTimeExtensions.PreviousPeriodOf(from, to);

        var current = await transactionRepository.GetTotalsAsync(from, to, cancellationToken);
        var previous = await transactionRepository.GetTotalsAsync(previousFrom, previousTo, cancellationToken);

        var creditCardCount = await context.CreditCards.CountAsync(c => c.IsActive, cancellationToken);
        var bankAccountCount = await context.BankAccounts.CountAsync(a => a.IsActive, cancellationToken);

        var ledgerCurrent = await GetLedgerTotalsAsync(from, to, cancellationToken);
        var ledgerPrevious = await GetLedgerTotalsAsync(previousFrom, previousTo, cancellationToken);
        var outstanding = await context.Persons.SumAsync(p => (decimal?)p.OutstandingBalance, cancellationToken) ?? 0m;

        var topCard = (await transactionRepository.GetTopSpendingCardsAsync(from, to, 1, cancellationToken)).FirstOrDefault();
        var topVendor = (await transactionRepository.GetTopSpendingVendorsAsync(from, to, 1, cancellationToken)).FirstOrDefault();
        var topBank = (await transactionRepository.GetMostActiveBanksAsync(from, to, 1, cancellationToken)).FirstOrDefault();
        var topPerson = await personRepository.GetMostActivePersonAsync(from, to, cancellationToken);

        var cards = new List<KpiCardDto>
        {
            BuildCard("totalCredit", "Total Credit", current.TotalCredit, previous.TotalCredit, "currency"),
            BuildCard("totalDebit", "Total Debit", current.TotalDebit, previous.TotalDebit, "currency", invertTrend: true),
            BuildCard("netAmount", "Net Amount", current.TotalCredit - current.TotalDebit, previous.TotalCredit - previous.TotalDebit, "currency"),
            BuildCard("totalTransactions", "Total Transactions", current.TransactionCount, previous.TransactionCount, "number"),
            BuildCard("totalVendors", "Total Vendors", current.VendorCount, previous.VendorCount, "number"),
            BuildCard("totalPersons", "Total Persons", current.PersonCount, previous.PersonCount, "number"),
            BuildCard("totalCreditCards", "Total Credit Cards", creditCardCount, creditCardCount, "number"),
            BuildCard("totalBankAccounts", "Total Bank Accounts", bankAccountCount, bankAccountCount, "number"),
            BuildCard("amountGiven", "Amount Given", ledgerCurrent.Given, ledgerPrevious.Given, "currency"),
            BuildCard("amountReceived", "Amount Received", ledgerCurrent.Taken, ledgerPrevious.Taken, "currency"),
            BuildCard("outstandingAmount", "Outstanding Amount", outstanding, outstanding, "currency",
                caption: outstanding >= 0 ? "Receivable from people" : "Payable to people")
        };

        return new DashboardSummaryDto(
            from,
            to,
            previousFrom,
            previousTo,
            AppConstants.DefaultCurrency,
            cards,
            topCard,
            topVendor,
            topBank,
            topPerson);
    }

    private async Task<(decimal Given, decimal Taken)> GetLedgerTotalsAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await context.MoneyLedgers
            .AsNoTracking()
            .Where(l => l.EntryDate >= from && l.EntryDate <= to)
            .GroupBy(l => l.EntryType)
            .Select(g => new { EntryType = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var given = rows.Where(r => r.EntryType == Domain.Enums.LedgerEntryType.Given).Sum(r => r.Total);
        var taken = rows.Where(r => r.EntryType == Domain.Enums.LedgerEntryType.Taken).Sum(r => r.Total);
        return (given, taken);
    }

    internal static (DateTime From, DateTime To) ResolveRange(GetDashboardSummaryQuery request, DateTime utcNow)
    {
        var today = utcNow.Date;
        return request.Period switch
        {
            DashboardPeriod.Today => (today, today.EndOfDay()),
            DashboardPeriod.Last7Days => (today.AddDays(-6), today.EndOfDay()),
            DashboardPeriod.Last30Days => (today.AddDays(-29), today.EndOfDay()),
            DashboardPeriod.Last90Days => (today.AddDays(-89), today.EndOfDay()),
            DashboardPeriod.Custom => (
                (request.From ?? today.AddDays(-29)).Date,
                (request.To ?? today).EndOfDay()),
            _ => (today.AddDays(-29), today.EndOfDay())
        };
    }

    private static KpiCardDto BuildCard(
        string key,
        string title,
        decimal value,
        decimal previousValue,
        string format,
        bool invertTrend = false,
        string? caption = null)
    {
        var difference = value - previousValue;
        decimal? percentChange = previousValue == 0m
            ? null
            : decimal.Round(difference / Math.Abs(previousValue) * 100m, 2);

        var trend = difference switch
        {
            > 0m => invertTrend ? "down" : "up",
            < 0m => invertTrend ? "up" : "down",
            _ => "flat"
        };

        return new KpiCardDto(key, title, value, format, previousValue, difference, percentChange, trend, caption);
    }
}
