using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Common.Specifications;

public static class SpecificationEvaluator
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, ISpecification<T> specification, bool ignorePaging = false)
        where T : class
    {
        if (specification.IgnoreQueryFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        if (specification.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        if (specification.AsSplitQuery)
        {
            query = query.AsSplitQuery();
        }

        if (specification.OrderExpressions.Count > 0)
        {
            var first = specification.OrderExpressions[0];
            var ordered = first.Descending
                ? query.OrderByDescending(first.KeySelector)
                : query.OrderBy(first.KeySelector);

            for (var i = 1; i < specification.OrderExpressions.Count; i++)
            {
                var next = specification.OrderExpressions[i];
                ordered = next.Descending
                    ? ordered.ThenByDescending(next.KeySelector)
                    : ordered.ThenBy(next.KeySelector);
            }

            query = ordered;
        }

        if (!ignorePaging && specification.IsPagingEnabled)
        {
            query = query.Skip(specification.Skip ?? 0).Take(specification.Take ?? int.MaxValue);
        }

        return query;
    }
}
