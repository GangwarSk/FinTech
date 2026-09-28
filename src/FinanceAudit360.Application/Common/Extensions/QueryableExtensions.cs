using System.Linq.Expressions;
using System.Reflection;
using FinanceAudit360.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Common.Extensions;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize is < 1 or > PaginationRequest.MaxPageSize ? PaginationRequest.DefaultPageSize : pageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, total, pageNumber, pageSize);
    }

    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition, Expression<Func<T, bool>> predicate) =>
        condition ? query.Where(predicate) : query;

    /// <summary>
    /// Applies a client-supplied sort column safely: the name is looked up against the CLR type
    /// and silently ignored when unknown, which prevents injection through the OrderBy clause.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sortBy, SortDirection direction, string fallbackProperty)
    {
        var property = ResolveProperty<T>(sortBy) ?? ResolveProperty<T>(fallbackProperty);
        if (property is null)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(parameter, property);
        var keySelector = Expression.Lambda(Expression.Convert(member, typeof(object)), parameter);

        var methodName = direction == SortDirection.Descending ? "OrderByDescending" : "OrderBy";
        var call = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(T), typeof(object)],
            query.Expression,
            Expression.Quote(keySelector));

        return query.Provider.CreateQuery<T>(call);
    }

    private static PropertyInfo? ResolveProperty<T>(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
}
