using System.Linq.Expressions;

namespace FinanceAudit360.Application.Common.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }

    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }

    IReadOnlyList<string> IncludeStrings { get; }

    IReadOnlyList<(Expression<Func<T, object>> KeySelector, bool Descending)> OrderExpressions { get; }

    int? Take { get; }

    int? Skip { get; }

    bool IsPagingEnabled { get; }

    bool AsNoTracking { get; }

    bool AsSplitQuery { get; }

    bool IgnoreQueryFilters { get; }
}
