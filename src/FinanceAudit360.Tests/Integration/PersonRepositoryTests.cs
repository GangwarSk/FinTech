using FinanceAudit360.Contracts.Persons;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Persistence.Repositories;

namespace FinanceAudit360.Tests.Integration;

public class PersonRepositoryTests : IAsyncLifetime
{
    private DatabaseFixture _fixture = null!;
    private PersonRepository _repository = null!;
    private Guid _raviId;

    public async ValueTask InitializeAsync()
    {
        _fixture = new DatabaseFixture();
        _repository = new PersonRepository(_fixture.Context);

        var ravi = Person.Create("Ravi Kumar", "9876543210", "ravi@example.com", "Friend");
        ravi.RecordEntry(new DateTime(2024, 1, 10), 10_000m, LedgerEntryType.Given, "Loan for business");
        ravi.RecordEntry(new DateTime(2024, 2, 15), 3_000m, LedgerEntryType.SettlementReceived, "Part repayment");
        ravi.RecordEntry(new DateTime(2024, 3, 20), 2_000m, LedgerEntryType.Taken, "Borrowed for rent");

        var priya = Person.Create("Priya Nair", "9123456780", "priya@example.com", "Colleague");
        priya.RecordEntry(new DateTime(2024, 2, 1), 8_000m, LedgerEntryType.Taken, "Borrowed");

        var inactive = Person.Create("Old Contact");
        inactive.SetActive(false);

        await _fixture.Context.Persons.AddRangeAsync([ravi, priya, inactive]);
        await _fixture.Context.SaveChangesAsync();

        _raviId = ravi.Id;
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task SearchAsync_ReturnsAggregatedBalances()
    {
        var page = await _repository.SearchAsync(new PersonFilterRequest());

        Assert.Equal(3, page.TotalCount);

        var ravi = page.Items.First(p => p.Name == "Ravi Kumar");
        Assert.Equal(10_000m, ravi.TotalGiven);
        Assert.Equal(2_000m, ravi.TotalTaken);

        // (10000 - 3000) - (2000 - 0) = 5000
        Assert.Equal(5_000m, ravi.OutstandingBalance);
        Assert.Equal(3, ravi.LedgerEntryCount);
    }

    [Fact]
    public async Task SearchAsync_FiltersByKeywordAcrossNameAndContact()
    {
        Assert.Equal(1, (await _repository.SearchAsync(new PersonFilterRequest { Keyword = "ravi" })).TotalCount);
        Assert.Equal(1, (await _repository.SearchAsync(new PersonFilterRequest { Keyword = "9123456780" })).TotalCount);
        Assert.Equal(1, (await _repository.SearchAsync(new PersonFilterRequest { Keyword = "colleague" })).TotalCount);
    }

    [Fact]
    public async Task SearchAsync_FiltersByActiveFlag() =>
        Assert.Equal(2, (await _repository.SearchAsync(new PersonFilterRequest { IsActive = true })).TotalCount);

    [Fact]
    public async Task SearchAsync_FiltersByOutstandingOnly() =>
        Assert.Equal(2, (await _repository.SearchAsync(new PersonFilterRequest { HasOutstanding = true })).TotalCount);

    [Fact]
    public async Task GetLedgerAsync_ReturnsSignedAmountsNewestFirst()
    {
        var page = await _repository.GetLedgerAsync(_raviId, new PersonLedgerFilterRequest());

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new DateTime(2024, 3, 20), page.Items[0].EntryDate);
        Assert.Equal(-2_000m, page.Items[0].SignedAmount);
    }

    [Fact]
    public async Task GetLedgerAsync_FiltersByEntryType()
    {
        var page = await _repository.GetLedgerAsync(_raviId, new PersonLedgerFilterRequest
        {
            EntryTypes = [(int)LedgerEntryType.Given]
        });

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(10_000m, page.Items[0].Amount);
    }

    [Fact]
    public async Task GetLedgerAsync_FiltersByDateRange()
    {
        var page = await _repository.GetLedgerAsync(_raviId, new PersonLedgerFilterRequest
        {
            DateFrom = new DateTime(2024, 2, 1),
            DateTo = new DateTime(2024, 2, 28)
        });

        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetAuditAsync_ReturnsTotalsAndTimeline()
    {
        var audit = await _repository.GetAuditAsync(_raviId, null, null);

        Assert.NotNull(audit);
        Assert.Equal(10_000m, audit.TotalGiven);
        Assert.Equal(2_000m, audit.TotalTaken);
        Assert.Equal(3_000m, audit.SettlementsReceived);
        Assert.Equal(5_000m, audit.Balance);
        Assert.Equal(3, audit.RecentEntries.Count);
        Assert.Equal(3, audit.Timeline.Count);
    }

    [Fact]
    public async Task GetAuditAsync_TimelineRunningBalanceAccumulates()
    {
        var audit = await _repository.GetAuditAsync(_raviId, null, null);

        Assert.Equal(10_000m, audit!.Timeline[0].RunningBalance);
        Assert.Equal(7_000m, audit.Timeline[1].RunningBalance);
        Assert.Equal(5_000m, audit.Timeline[2].RunningBalance);
    }

    [Fact]
    public async Task GetAuditAsync_UnknownPerson_ReturnsNull() =>
        Assert.Null(await _repository.GetAuditAsync(Guid.CreateVersion7(), null, null));

    [Fact]
    public async Task GetOutstandingAsync_OrdersByAbsoluteBalance()
    {
        var outstanding = await _repository.GetOutstandingAsync(10);

        Assert.Equal(2, outstanding.Count);

        // Priya owes the larger magnitude (-8000) so she leads, even though the balance is negative.
        Assert.Equal("Priya Nair", outstanding[0].PersonName);
        Assert.Equal(-8_000m, outstanding[0].Outstanding);
        Assert.Equal("Ravi Kumar", outstanding[1].PersonName);
        Assert.Equal(5_000m, outstanding[1].Outstanding);
    }

    [Fact]
    public async Task MatchByKeywordAsync_LinksNarrationsToPeople()
    {
        var person = await _repository.GetByIdAsync(_raviId, asNoTracking: false);
        person!.Update("Ravi Kumar", "9876543210", "ravi@example.com", person.Address, "Friend", null, "RAVI KUMAR|RAVIK");
        await _fixture.Context.SaveChangesAsync();

        var matched = await _repository.MatchByKeywordAsync("UPI/RAVIK/PAYMENT/123456");

        Assert.NotNull(matched);
        Assert.Equal(_raviId, matched.Id);
    }

    [Fact]
    public async Task MatchByKeywordAsync_ReturnsNullWhenNothingMatches() =>
        Assert.Null(await _repository.MatchByKeywordAsync("UPI/UNKNOWN/PAYMENT"));
}
