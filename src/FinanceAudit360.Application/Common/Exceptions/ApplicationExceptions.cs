namespace FinanceAudit360.Application.Common.Exceptions;

public sealed class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
        => Errors = new Dictionary<string, string[]>(errors);

    public ValidationException(string propertyName, string errorMessage)
        : base("One or more validation errors occurred.")
        => Errors = new Dictionary<string, string[]> { [propertyName] = [errorMessage] };

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class NotFoundException(string entityName, object key)
    : Exception($"{entityName} with key '{key}' was not found.")
{
    public string EntityName { get; } = entityName;

    public object Key { get; } = key;
}

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ForbiddenAccessException(string message = "You are not allowed to perform this action.")
    : Exception(message);

public sealed class UnauthorizedException(string message = "Authentication is required.") : Exception(message);
