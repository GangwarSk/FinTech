using System.Linq.Expressions;
using FinanceAudit360.Application.Common.Specifications;
using FinanceAudit360.Domain.Common;
using FinanceAudit360.Shared.Models;

namespace FinanceAudit360.Application.Common.Interfaces;

/// <summary>
/// Simple-query half of the hybrid repository pattern: CRUD plus specification execution
/// for any aggregate root. Anything more complex belongs in a feature repository.
/// </summary>
public interface IGenericRepository<T> where T : AuditableEntity, IAggregateRoot
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<T?> GetByIdAsync(Guid id, bool asNoTracking, CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

    Task<PagedResult<T>> PagedListAsync(ISpecification<T> specification, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);

    /// <summary>Flags the entity as deleted; the SaveChanges interceptor converts this to an UPDATE.</summary>
    void SoftDelete(T entity, string? deletedBy);

    IQueryable<T> Query(bool asNoTracking = true);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    IGenericRepository<T> Repository<T>() where T : AuditableEntity, IAggregateRoot;
}
