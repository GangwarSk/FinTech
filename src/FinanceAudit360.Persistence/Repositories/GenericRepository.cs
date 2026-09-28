using System.Linq.Expressions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Specifications;
using FinanceAudit360.Domain.Common;
using FinanceAudit360.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Persistence.Repositories;

public class GenericRepository<T>(ApplicationDbContext context) : IGenericRepository<T>
    where T : AuditableEntity, IAggregateRoot
{
    protected ApplicationDbContext Context { get; } = context;

    protected DbSet<T> Set => Context.Set<T>();

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, asNoTracking: true, cancellationToken);

    public Task<T?> GetByIdAsync(Guid id, bool asNoTracking, CancellationToken cancellationToken = default)
    {
        var query = asNoTracking ? Set.AsNoTracking() : Set.AsQueryable();
        return query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) =>
        SpecificationEvaluator.Apply(Set.AsQueryable(), specification).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) =>
        await SpecificationEvaluator.Apply(Set.AsQueryable(), specification).ToListAsync(cancellationToken);

    public async Task<PagedResult<T>> PagedListAsync(
        ISpecification<T> specification,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = SpecificationEvaluator.Apply(Set.AsQueryable(), specification, ignorePaging: true);
        var total = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, total, pageNumber, pageSize);
    }

    public Task<int> CountAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default) =>
        specification is null
            ? Set.CountAsync(cancellationToken)
            : SpecificationEvaluator.Apply(Set.AsQueryable(), specification, ignorePaging: true).CountAsync(cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(predicate, cancellationToken);

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        Set.AddRangeAsync(entities, cancellationToken);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);

    public void SoftDelete(T entity, string? deletedBy)
    {
        entity.MarkDeleted(deletedBy, DateTime.UtcNow);
        Set.Update(entity);
    }

    public IQueryable<T> Query(bool asNoTracking = true) => asNoTracking ? Set.AsNoTracking() : Set.AsQueryable();
}
