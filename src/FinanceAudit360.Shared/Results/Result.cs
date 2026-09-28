using System.Diagnostics.CodeAnalysis;

namespace FinanceAudit360.Shared.Results;

public class Result
{
    protected Result(bool isSuccess, Error error, IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(false, Error.Validation("validation_failed", "One or more validation errors occurred."), errors);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Create(value);

    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.CreateFailure(error);

    public static Result<TValue> Invalid<TValue>(IReadOnlyDictionary<string, string[]> errors) =>
        Result<TValue>.CreateInvalid(errors);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(TValue? value, bool isSuccess, Error error, IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : base(isSuccess, error, validationErrors)
        => _value = value;

    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public TValue? ValueOrDefault => _value;

    internal static Result<TValue> Create(TValue value) => new(value, true, Error.None);

    internal static Result<TValue> CreateFailure(Error error) => new(default, false, error);

    internal static Result<TValue> CreateInvalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(default, false, Error.Validation("validation_failed", "One or more validation errors occurred."), errors);

    public static implicit operator Result<TValue>(TValue value) => Create(value);

    public Result<TOut> Map<TOut>(Func<TValue, TOut> mapper) =>
        IsSuccess ? Result<TOut>.Create(mapper(Value)) : Result<TOut>.CreateFailure(Error);
}
