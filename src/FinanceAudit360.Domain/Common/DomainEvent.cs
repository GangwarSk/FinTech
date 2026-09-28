using MediatR;

namespace FinanceAudit360.Domain.Common;

/// <summary>Marker for anything raised by an aggregate and dispatched after a successful SaveChanges.</summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
