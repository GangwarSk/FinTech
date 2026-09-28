namespace FinanceAudit360.Domain.Common;

public abstract class Entity : IEquatable<Entity>
{
    protected Entity() => Id = Guid.CreateVersion7();

    protected Entity(Guid id) => Id = id;

    public Guid Id { get; protected set; }

    public bool Equals(Entity? other) =>
        other is not null && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    public override bool Equals(object? obj) => obj is Entity entity && Equals(entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}

/// <summary>Consistency boundary. Only aggregate roots may be loaded directly from a repository.</summary>
public interface IAggregateRoot;

public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTime? DeletedOnUtc { get; }

    string? DeletedBy { get; }

    void MarkDeleted(string? deletedBy, DateTime utcNow);

    void Restore();
}

public interface IAuditable
{
    DateTime CreatedOnUtc { get; }

    string? CreatedBy { get; }

    DateTime? ModifiedOnUtc { get; }

    string? ModifiedBy { get; }
}
