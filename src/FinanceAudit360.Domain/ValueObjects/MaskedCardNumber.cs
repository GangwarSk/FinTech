using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.ValueObjects;

/// <summary>
/// A card number is never stored in full. Only the masked form and the last four digits are persisted,
/// which is what every bank statement exposes anyway.
/// </summary>
public sealed class MaskedCardNumber : ValueObject
{
    private MaskedCardNumber(string masked, string last4)
    {
        Masked = masked;
        Last4 = last4;
    }

    public string Masked { get; }

    public string Last4 { get; }

    public static MaskedCardNumber Create(string? rawOrMasked)
    {
        var last4 = rawOrMasked.Last4();
        if (last4.Length != 4)
        {
            throw new DomainException("card.number_invalid", "Card number must expose at least the last 4 digits.");
        }

        var digitCount = rawOrMasked.DigitsOnly().Length;
        var maskLength = digitCount > 4 ? digitCount - 4 : 12;
        return new MaskedCardNumber(new string('X', maskLength) + last4, last4);
    }

    public static MaskedCardNumber FromLast4(string last4)
    {
        var digits = last4.DigitsOnly();
        if (digits.Length != 4)
        {
            throw new DomainException("card.number_invalid", "Exactly 4 digits are required.");
        }

        return new MaskedCardNumber(new string('X', 12) + digits, digits);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Masked;
        yield return Last4;
    }

    public override string ToString() => Masked;
}
