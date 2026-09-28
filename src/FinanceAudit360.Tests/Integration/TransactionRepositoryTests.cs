using FinanceAudit360.Contracts.Transactions;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Persistence.Repositories;
using FinanceAudit360.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Tests.Integration;

public class TransactionRepositoryTests : IAsyncLifetime
{
    private DatabaseFixture _fixture = null!;
    private TransactionRepository _repository = null!;
    private Guid _bankId;
    private Guid _cardId;
    private Guid _accountId;
    private Guid _vendorId;

    public async ValueTask InitializeAsync()
    {
        _fixture = new DatabaseFixture();
        _repository = new TransactionRepository(_fixture.Context);

        var (bank, card, account) = await _fixture.SeedMastersAsync();
        _bankId = bank.Id;
        _cardId = card.Id;
        _accountId = account.Id;

        var vendor = Vendor.Create("Amazon", "AMAZON|AMZN");
        await _fixture.Context.Vendors.AddAsync(vendor);
        await _fixture.Context.SaveChangesAsync();
        _vendorId = vendor.Id;

        await _fixture.AddTransactionAsync(new DateTime(2024, 5, 2), 845m, TransactionDirection.Debit, "SWIGGY BANGALORE", _bankId, _cardId);
        await _fixture.AddTransactionAsync(new DateTime(2024, 5, 5), 3250.50m, TransactionDirection.Debit, "AMAZON RETAIL", _bankId, _cardId, vendorId: _vendorId);
        await _fixture.AddTransactionAsync(new DateTime(2024, 5, 10), 12500m, TransactionDirection.Credit, "PAYMENT RECEIVED", _bankId, _cardId, type: TransactionType.Payment);
        await _fixture.AddTransactionAsync(new DateTime(2024, 6, 1), 2000m, TransactionDirection.Debit, "ELECTRICITY BILL", _bankId, accountId: _accountId);
        await _fixture.AddTransactionAsync(new DateTime(2024, 6, 15), 50000m, TransactionDirection.Credit, "SALARY CREDIT", _bankId, accountId: _accountId, type: TransactionType.Credit);
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task SearchAsync_WithoutFilters_ReturnsEverythingWithTotals()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest());

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(62_500m, result.TotalCredit);
        Assert.Equal(6_095.50m, result.TotalDebit);
        Assert.Equal(56_404.50m, result.NetAmount);
    }

    [Fact]
    public async Task SearchAsync_FiltersByDateRange()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest
        {
            DateFrom = new DateTime(2024, 6, 1),
            DateTo = new DateTime(2024, 6, 30)
        });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_CreditOnly_ExcludesDebits()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest { CreditOnly = true });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(0m, result.TotalDebit);
        Assert.All(result.Items, item => Assert.Equal("Credit", item.Direction));
    }

    [Fact]
    public async Task SearchAsync_FiltersByAmountRange()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest
        {
            AmountFrom = 1000m,
            AmountTo = 5000m
        });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_FiltersByCard()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest { CreditCardIds = [_cardId] });

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(_cardId, item.CreditCardId));
    }

    [Fact]
    public async Task SearchAsync_FiltersByAccount()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest { BankAccountIds = [_accountId] });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_KeywordSearchesAcrossDescriptionAndVendor()
    {
        var byDescription = await _repository.SearchAsync(new TransactionFilterRequest { Keyword = "swiggy" });
        Assert.Equal(1, byDescription.TotalCount);

        var byVendor = await _repository.SearchAsync(new TransactionFilterRequest { Keyword = "amazon" });
        Assert.Equal(1, byVendor.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_CombinesFiltersWithAnd()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest
        {
            CreditCardIds = [_cardId],
            DebitOnly = true,
            AmountFrom = 1000m
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Contains("AMAZON", result.Items[0].Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_FiltersByTransactionType()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest
        {
            TransactionTypes = [(int)TransactionType.Payment]
        });

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_PagesResults()
    {
        var page1 = await _repository.SearchAsync(new TransactionFilterRequest { PageNumber = 1, PageSize = 2 });
        var page2 = await _repository.SearchAsync(new TransactionFilterRequest { PageNumber = 2, PageSize = 2 });

        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_SortsByAmountAscending()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest
        {
            SortBy = nameof(Transaction.Amount),
            SortDirection = SortDirection.Ascending
        });

        Assert.Equal(845m, result.Items[0].Amount);
    }

    [Fact]
    public async Task SearchAsync_RejectsUnknownSortColumnsSilently()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest { SortBy = "DROP TABLE Transactions" });

        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_GroupsByMonth()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest { GroupBy = "month" });

        Assert.Equal(2, result.Groups.Count);
        Assert.Contains(result.Groups, g => g.Key == "2024-05");
        Assert.Contains(result.Groups, g => g.Key == "2024-06");
    }

    [Fact]
    public async Task SearchAsync_GroupsByCardWithResolvedLabels()
    {
        var result = await _repository.SearchAsync(new TransactionFilterRequest { GroupBy = "card" });

        var cardGroup = result.Groups.First(g => g.Key == _cardId.ToString());
        Assert.Equal(3, cardGroup.Count);
        Assert.Equal("HDFC Regalia", cardGroup.Label);
    }

    [Fact]
    public async Task GetTotalsAsync_AggregatesTheWindow()
    {
        var totals = await _repository.GetTotalsAsync(new DateTime(2024, 5, 1), new DateTime(2024, 5, 31));

        Assert.Equal(3, totals.TransactionCount);
        Assert.Equal(12_500m, totals.TotalCredit);
        Assert.Equal(4_095.50m, totals.TotalDebit);
    }

    [Fact]
    public async Task GetTopSpendingCardsAsync_ResolvesTheCardName()
    {
        var top = await _repository.GetTopSpendingCardsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), 5);

        Assert.Single(top);
        Assert.Equal("HDFC Regalia", top[0].Name);
        Assert.Equal(4_095.50m, top[0].Amount);
    }

    [Fact]
    public async Task GetMonthlyActivityAsync_ReturnsOneRowPerMonthInOrder()
    {
        var months = await _repository.GetMonthlyActivityAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

        Assert.Equal(2, months.Count);
        Assert.Equal(5, months[0].Month);
        Assert.Equal(6, months[1].Month);
    }

    [Fact]
    public async Task SoftDelete_RemovesTheRowFromEveryQuery()
    {
        var transaction = await _fixture.Context.Transactions.FirstAsync();

        _repository.SoftDelete(transaction, "tester");
        await _fixture.Context.SaveChangesAsync();

        var result = await _repository.SearchAsync(new TransactionFilterRequest());
        Assert.Equal(4, result.TotalCount);

        var stillInTable = await _fixture.Context.Transactions
            .IgnoreQueryFilters()
            .CountAsync(t => t.Id == transaction.Id);

        Assert.Equal(1, stillInTable);
    }

    [Fact]
    public async Task BulkAssignAsync_UpdatesEveryRequestedRow()
    {
        var ids = await _fixture.Context.Transactions.Select(t => t.Id).Take(3).ToListAsync();

        var affected = await _repository.BulkAssignAsync(new BulkAssignRequest(ids, _vendorId, null, null), "tester");

        Assert.Equal(3, affected);
        Assert.Equal(3, await _fixture.Context.Transactions.CountAsync(t => t.VendorId == _vendorId));
    }

    [Fact]
    public async Task GetExistingDedupeHashesAsync_DetectsAlreadyImportedLines()
    {
        var existing = await _fixture.Context.Transactions.Select(t => t.DedupeHash).Take(2).ToListAsync();

        var found = await _repository.GetExistingDedupeHashesAsync([.. existing, "not-a-real-hash"]);

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public async Task AuditColumns_ArePopulatedByTheInterceptor()
    {
        var transaction = await _fixture.Context.Transactions.AsNoTracking().FirstAsync();

        Assert.Equal("tester", transaction.CreatedBy);
        Assert.NotEqual(default, transaction.CreatedOnUtc);
    }
}
