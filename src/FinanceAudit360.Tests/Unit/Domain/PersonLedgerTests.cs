using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Exceptions;

namespace FinanceAudit360.Tests.Unit.Domain;

public class PersonLedgerTests
{
    [Fact]
    public void RecordEntry_Given_IncreasesWhatThePersonOwesMe()
    {
        var person = Person.Create("Ravi Kumar");

        person.RecordEntry(new DateTime(2024, 1, 10), 5000m, LedgerEntryType.Given, "Cash lent");

        Assert.Equal(5000m, person.TotalGiven);
        Assert.Equal(0m, person.TotalTaken);
        Assert.Equal(5000m, person.OutstandingBalance);
    }

    [Fact]
    public void RecordEntry_Taken_MakesTheBalanceNegative()
    {
        var person = Person.Create("Ravi Kumar");

        person.RecordEntry(new DateTime(2024, 1, 10), 2000m, LedgerEntryType.Taken, "Borrowed");

        Assert.Equal(2000m, person.TotalTaken);
        Assert.Equal(-2000m, person.OutstandingBalance);
    }

    [Fact]
    public void SettlementReceived_ReducesTheOutstandingBalance()
    {
        var person = Person.Create("Ravi Kumar");

        person.RecordEntry(new DateTime(2024, 1, 10), 5000m, LedgerEntryType.Given, "Cash lent");
        person.RecordEntry(new DateTime(2024, 2, 10), 3000m, LedgerEntryType.SettlementReceived, "Part repayment");

        Assert.Equal(5000m, person.TotalGiven);
        Assert.Equal(2000m, person.OutstandingBalance);
    }

    [Fact]
    public void SettlementPaid_ReducesWhatIOwe()
    {
        var person = Person.Create("Ravi Kumar");

        person.RecordEntry(new DateTime(2024, 1, 10), 4000m, LedgerEntryType.Taken, "Borrowed");
        person.RecordEntry(new DateTime(2024, 3, 10), 1500m, LedgerEntryType.SettlementPaid, "Repaid part");

        Assert.Equal(-2500m, person.OutstandingBalance);
    }

    [Fact]
    public void MixedEntries_NetOutCorrectly()
    {
        var person = Person.Create("Ravi Kumar");

        person.RecordEntry(new DateTime(2024, 1, 1), 10_000m, LedgerEntryType.Given, "Loan");
        person.RecordEntry(new DateTime(2024, 2, 1), 4_000m, LedgerEntryType.Taken, "Borrowed back");
        person.RecordEntry(new DateTime(2024, 3, 1), 2_500m, LedgerEntryType.SettlementReceived, "Repayment");
        person.RecordEntry(new DateTime(2024, 4, 1), 1_000m, LedgerEntryType.SettlementPaid, "Settled");

        // (10000 - 2500) - (4000 - 1000) = 4500
        Assert.Equal(4_500m, person.OutstandingBalance);
        Assert.Equal(new DateTime(2024, 4, 1), person.LastActivityOn);
    }

    [Fact]
    public void RemoveEntry_ExcludesItFromTheTotals()
    {
        var person = Person.Create("Ravi Kumar");
        var entry = person.RecordEntry(new DateTime(2024, 1, 10), 5000m, LedgerEntryType.Given, "Cash lent");
        person.RecordEntry(new DateTime(2024, 1, 20), 1000m, LedgerEntryType.Given, "More cash");

        person.RemoveEntry(entry.Id, "tester", DateTime.UtcNow);

        Assert.Equal(1000m, person.TotalGiven);
        Assert.Equal(1000m, person.OutstandingBalance);
    }

    [Fact]
    public void RemoveEntry_UnknownId_Throws()
    {
        var person = Person.Create("Ravi Kumar");

        Assert.Throws<EntityNotFoundException>(() =>
            person.RemoveEntry(Guid.CreateVersion7(), "tester", DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void RecordEntry_NonPositiveAmount_Throws(decimal amount)
    {
        var person = Person.Create("Ravi Kumar");

        Assert.Throws<DomainException>(() =>
            person.RecordEntry(DateTime.UtcNow, amount, LedgerEntryType.Given, "Invalid"));
    }

    [Fact]
    public void Create_NormalisesContactDetails()
    {
        var person = Person.Create("  Ravi   Kumar ", "+91 98765-43210", "RAVI@Example.COM", "Friend");

        Assert.Equal("Ravi Kumar", person.Name);
        Assert.Equal("RAVI KUMAR", person.NormalizedName);
        Assert.Equal("919876543210", person.Mobile);
        Assert.Equal("ravi@example.com", person.Email);
    }

    [Fact]
    public void Create_InvalidEmail_IsDroppedRatherThanThrowing()
    {
        var person = Person.Create("Ravi Kumar", email: "not-an-email");

        Assert.Null(person.Email);
    }
}
