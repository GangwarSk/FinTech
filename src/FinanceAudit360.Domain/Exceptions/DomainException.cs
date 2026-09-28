namespace FinanceAudit360.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string code, string message) : base(message) => Code = code;

    public DomainException(string code, string message, Exception inner) : base(message, inner) => Code = code;

    public string Code { get; }
}

public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object key)
        : base("entity.not_found", $"{entityName} with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }

    public object Key { get; }
}

public sealed class BusinessRuleViolationException(string code, string message) : DomainException(code, message);
