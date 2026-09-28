using System.Collections.Concurrent;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Persistence.Repositories;

public sealed class UnitOfWork(ApplicationDbContext context, IServiceProvider serviceProvider) : IUnitOfWork
{
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public IGenericRepository<T> Repository<T>() where T : AuditableEntity, IAggregateRoot =>
        (IGenericRepository<T>)_repositories.GetOrAdd(
            typeof(T),
            _ => serviceProvider.GetService(typeof(IGenericRepository<T>)) ?? new GenericRepository<T>(context));

    /// <summary>
    /// Runs the operation inside an execution strategy so a transient SQL failure retries the
    /// whole transaction rather than replaying half of it.
    /// </summary>
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(token);
            try
            {
                var result = await operation(token);
                await context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(token);
                throw;
            }
        }, cancellationToken);
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async token =>
        {
            await operation(token);
            return true;
        }, cancellationToken);
}
