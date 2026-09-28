using System.Linq.Expressions;

namespace FinanceAudit360.Application.Common.Specifications;

public abstract class BaseSpecification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, object>>> _includes = [];
    private readonly List<string> _includeStrings = [];
    private readonly List<(Expression<Func<T, object>>, bool)> _orderExpressions = [];

    protected BaseSpecification()
    {
    }

    protected BaseSpecification(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    public Expression<Func<T, bool>>? Criteria { get; private set; }

    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;

    public IReadOnlyList<string> IncludeStrings => _includeStrings;

    public IReadOnlyList<(Expression<Func<T, object>> KeySelector, bool Descending)> OrderExpressions => _orderExpressions;

    public int? Take { get; private set; }

    public int? Skip { get; private set; }

    public bool IsPagingEnabled { get; private set; }

    public bool AsNoTracking { get; private set; } = true;

    public bool AsSplitQuery { get; private set; }

    public bool IgnoreQueryFilters { get; private set; }

    protected void Where(Expression<Func<T, bool>> criteria) =>
        Criteria = Criteria is null ? criteria : Criteria.AndAlso(criteria);

    /// <summary>ANDs the predicate only when <paramref name="condition"/> holds - keeps filter builders flat.</summary>
    protected void WhereIf(bool condition, Expression<Func<T, bool>> criteria)
    {
        if (condition)
        {
            Where(criteria);
        }
    }

    protected void Or(Expression<Func<T, bool>> criteria) =>
        Criteria = Criteria is null ? criteria : Criteria.OrElse(criteria);

    protected void AddInclude(Expression<Func<T, object>> include) => _includes.Add(include);

    protected void AddInclude(string includeString) => _includeStrings.Add(includeString);

    protected void OrderBy(Expression<Func<T, object>> keySelector) => _orderExpressions.Add((keySelector, false));

    protected void OrderByDescending(Expression<Func<T, object>> keySelector) => _orderExpressions.Add((keySelector, true));

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
    }

    protected void ApplyTracking() => AsNoTracking = false;

    protected void ApplySplitQuery() => AsSplitQuery = true;

    protected void ApplyIgnoreQueryFilters() => IgnoreQueryFilters = true;
}
