using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinanceAudit360.Persistence.Interceptors;

/// <summary>
/// Stamps auditing columns and converts hard deletes of soft-deletable entities into updates,
/// so no call site has to remember to do it.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentUser currentUser, IDateTimeProvider dateTime)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var utcNow = dateTime.UtcNow;
        var user = currentUser.UserName ?? "system";

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreated(user, utcNow);
                    break;

                case EntityState.Modified:
                    entry.Entity.SetModified(user, utcNow);
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.MarkDeleted(user, utcNow);
                    entry.Entity.SetModified(user, utcNow);
                    break;

                case EntityState.Unchanged when HasChangedOwnedEntities(entry):
                    entry.Entity.SetModified(user, utcNow);
                    break;
            }
        }
    }

    /// <summary>Owned value objects do not mark the principal as modified on their own.</summary>
    private static bool HasChangedOwnedEntities(EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry is { } target &&
            target.Metadata.IsOwned() &&
            target.State is EntityState.Added or EntityState.Modified);
}
