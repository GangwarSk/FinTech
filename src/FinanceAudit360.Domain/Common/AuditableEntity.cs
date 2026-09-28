using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceAudit360.Domain.Common;

/// <summary>
/// Base for every persisted entity: auditing columns, soft delete, optimistic concurrency token
/// and the domain-event outbox consumed by the SaveChanges interceptor.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditable, ISoftDeletable
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid id) : base(id)
    {
    }

    public DateTime CreatedOnUtc { get; private set; } = DateTime.UtcNow;

    public string? CreatedBy { get; private set; }

    public DateTime? ModifiedOnUtc { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedOnUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    /// <summary>Legacy SQL Server rowversion. Not mapped on PostgreSQL, where the system "xmin" column is the concurrency token.</summary>
    public byte[]? RowVersion { get; private set; }

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void SetCreated(string? user, DateTime utcNow)
    {
        CreatedBy = user;
        CreatedOnUtc = utcNow;
    }

    public void SetModified(string? user, DateTime utcNow)
    {
        ModifiedBy = user;
        ModifiedOnUtc = utcNow;
    }

    public void MarkDeleted(string? deletedBy, DateTime utcNow)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedOnUtc = utcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedBy = null;
        DeletedOnUtc = null;
    }

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
