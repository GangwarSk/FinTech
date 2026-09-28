using System.Runtime.CompilerServices;

namespace FinanceAudit360.Shared.Guards;

public static class Guard
{
    public static T NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
        => value ?? throw new ArgumentNullException(name);

    public static string NotNullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or whitespace.", name)
            : value;

    public static Guid NotEmpty(Guid value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => value == Guid.Empty ? throw new ArgumentException("Value cannot be an empty GUID.", name) : value;

    public static decimal NotNegative(decimal value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => value < 0 ? throw new ArgumentOutOfRangeException(name, value, "Value cannot be negative.") : value;

    public static decimal GreaterThanZero(decimal value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => value <= 0 ? throw new ArgumentOutOfRangeException(name, value, "Value must be greater than zero.") : value;

    public static int InRange(int value, int min, int max, [CallerArgumentExpression(nameof(value))] string? name = null)
        => value < min || value > max
            ? throw new ArgumentOutOfRangeException(name, value, $"Value must be between {min} and {max}.")
            : value;

    public static string MaxLength(string value, int maxLength, [CallerArgumentExpression(nameof(value))] string? name = null)
        => value.Length > maxLength
            ? throw new ArgumentException($"Value exceeds the maximum length of {maxLength}.", name)
            : value;
}
