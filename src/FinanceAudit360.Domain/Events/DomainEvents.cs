using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;

namespace FinanceAudit360.Domain.Events;

public sealed record UserRegisteredEvent(Guid UserId, string UserName, string Email) : DomainEvent;

public sealed record UserLoggedInEvent(Guid UserId, string UserName, DateTime LoggedInOnUtc) : DomainEvent;

public sealed record TransactionCreatedEvent(
    Guid TransactionId,
    decimal Amount,
    TransactionDirection Direction,
    DateTime TransactionDate) : DomainEvent;

public sealed record StatementImportedEvent(
    Guid StatementId,
    Guid BankId,
    Guid? CreditCardId,
    Guid? BankAccountId,
    int TransactionCount,
    decimal TotalCredits,
    decimal TotalDebits) : DomainEvent;

public sealed record PersonLedgerChangedEvent(Guid PersonId, decimal OutstandingBalance) : DomainEvent;
