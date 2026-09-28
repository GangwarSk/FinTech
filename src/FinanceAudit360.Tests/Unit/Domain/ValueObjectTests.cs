using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Domain.ValueObjects;

namespace FinanceAudit360.Tests.Unit.Domain;

public class ValueObjectTests
{
    [Fact]
    public void Money_RoundsToTwoDecimalsAwayFromZero()
    {
        Assert.Equal(10.13m, Money.Of(10.125m).Amount);
        Assert.Equal(-10.13m, Money.Of(-10.125m).Amount);
    }

    [Fact]
    public void Money_AddAndSubtract_KeepCurrency()
    {
        var total = Money.Of(100m).Add(Money.Of(50.55m)).Subtract(Money.Of(0.55m));

        Assert.Equal(150m, total.Amount);
        Assert.Equal("INR", total.Currency);
    }

    [Fact]
    public void Money_MixedCurrencies_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => Money.Of(10m, "INR").Add(Money.Of(10m, "USD")));

        Assert.Equal("money.currency_mismatch", exception.Code);
    }

    [Fact]
    public void Money_EqualityIsByValue() =>
        Assert.Equal(Money.Of(25m, "INR"), Money.Of(25m, "INR"));

    [Theory]
    [InlineData("4111111111111234", "1234")]
    [InlineData("XXXX XXXX XXXX 9876", "9876")]
    [InlineData("5678", "5678")]
    public void MaskedCardNumber_NeverRetainsTheFullNumber(string input, string expectedLast4)
    {
        var card = MaskedCardNumber.Create(input);

        Assert.Equal(expectedLast4, card.Last4);
        Assert.EndsWith(expectedLast4, card.Masked, StringComparison.Ordinal);
        Assert.Equal(expectedLast4, card.Masked[^4..]);
        Assert.All(card.Masked[..^4], character => Assert.Equal('X', character));
    }

    [Fact]
    public void MaskedCardNumber_TooShort_Throws() =>
        Assert.Throws<DomainException>(() => MaskedCardNumber.Create("12"));

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("First.Last+tag@sub.domain.co.in")]
    public void EmailAddress_AcceptsValidAddresses(string value) =>
        Assert.Equal(value.ToLowerInvariant(), EmailAddress.Create(value).Value);

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("missing@tld")]
    [InlineData("two@@at.com")]
    public void EmailAddress_RejectsInvalidAddresses(string value) =>
        Assert.Throws<DomainException>(() => EmailAddress.Create(value));

    [Fact]
    public void DateRange_EndBeforeStart_Throws() =>
        Assert.Throws<DomainException>(() => DateRange.Create(new DateTime(2024, 5, 10), new DateTime(2024, 5, 1)));

    [Fact]
    public void DateRange_ComputesInclusiveDayCount() =>
        Assert.Equal(31, DateRange.Create(new DateTime(2024, 5, 1), new DateTime(2024, 5, 31)).DayCount);

    [Fact]
    public void DateRange_DetectsOverlap()
    {
        var may = DateRange.Create(new DateTime(2024, 5, 1), new DateTime(2024, 5, 31));
        var mid = DateRange.Create(new DateTime(2024, 5, 15), new DateTime(2024, 6, 15));
        var july = DateRange.Create(new DateTime(2024, 7, 1), new DateTime(2024, 7, 31));

        Assert.True(may.Overlaps(mid));
        Assert.False(may.Overlaps(july));
    }
}
