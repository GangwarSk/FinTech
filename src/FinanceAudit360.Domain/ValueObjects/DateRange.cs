using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Exceptions;

namespace FinanceAudit360.Domain.ValueObjects;

public sealed class DateRange : ValueObject
{
    private DateRange(DateTime from, DateTime to)
    {
        From = from;
        To = to;
    }

    public DateTime From { get; }

    public DateTime To { get; }

    public int DayCount => (int)(To.Date - From.Date).TotalDays + 1;

    public static DateRange Create(DateTime from, DateTime to)
    {
        if (to < from)
        {
            throw new DomainException("daterange.invalid", "The end date must not precede the start date.");
        }

        return new DateRange(from, to);
    }

    public bool Contains(DateTime value) => value >= From && value <= To;

    public bool Overlaps(DateRange other) => From <= other.To && other.From <= To;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return From;
        yield return To;
    }

    public override string ToString() => $"{From:yyyy-MM-dd} - {To:yyyy-MM-dd}";
}

public sealed class Address : ValueObject
{
    private Address(string? line1, string? line2, string? city, string? state, string? postalCode, string? country)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public string? Line1 { get; }

    public string? Line2 { get; }

    public string? City { get; }

    public string? State { get; }

    public string? PostalCode { get; }

    public string? Country { get; }

    public static Address Empty => new(null, null, null, null, null, null);

    public static Address Create(
        string? line1,
        string? line2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? country = "India") => new(line1, line2, city, state, postalCode, country);

    public bool IsEmpty => string.IsNullOrWhiteSpace(Line1) && string.IsNullOrWhiteSpace(Line2) &&
                           string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(State) &&
                           string.IsNullOrWhiteSpace(PostalCode);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }

    public override string ToString() =>
        string.Join(", ", new[] { Line1, Line2, City, State, PostalCode, Country }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
}
