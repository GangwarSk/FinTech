using System.Text.RegularExpressions;
using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.ValueObjects;

public sealed partial class EmailAddress : ValueObject
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static EmailAddress Create(string value)
    {
        var normalized = value.NormalizeText().ToLowerInvariant();
        if (!Pattern().IsMatch(normalized))
        {
            throw new DomainException("email.invalid", $"'{value}' is not a valid email address.");
        }

        return new EmailAddress(normalized);
    }

    public static EmailAddress? CreateOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) || !Pattern().IsMatch(value.NormalizeText().ToLowerInvariant())
            ? null
            : Create(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

public sealed class PhoneNumber : ValueObject
{
    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static PhoneNumber Create(string value)
    {
        var digits = value.DigitsOnly();
        if (digits.Length is < 7 or > 15)
        {
            throw new DomainException("phone.invalid", $"'{value}' is not a valid phone number.");
        }

        return new PhoneNumber(digits);
    }

    public static PhoneNumber? CreateOrNull(string? value)
    {
        var digits = value.DigitsOnly();
        return digits.Length is < 7 or > 15 ? null : new PhoneNumber(digits);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
