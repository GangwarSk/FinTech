using System.Linq.Expressions;

namespace FinanceAudit360.Application.Common.Specifications;

/// <summary>
/// Combines predicates by rewriting the second expression's parameter to the first one's.
/// Using Expression.Invoke instead would break EF Core's SQL translation on some providers.
/// </summary>
public static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> AndAlso<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        => Combine(left, right, Expression.AndAlso);

    public static Expression<Func<T, bool>> OrElse<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        => Combine(left, right, Expression.OrElse);

    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression)
        => Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var parameter = left.Parameters[0];
        var rewrittenRight = new ParameterRebinder(right.Parameters[0], parameter).Visit(right.Body)!;
        return Expression.Lambda<Func<T, bool>>(merge(left.Body, rewrittenRight), parameter);
    }

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}

/// <summary>Fluent builder used by the search engine to assemble AND/OR predicate trees at runtime.</summary>
public sealed class PredicateBuilder<T>
{
    private Expression<Func<T, bool>>? _predicate;

    public static PredicateBuilder<T> Create() => new();

    public PredicateBuilder<T> And(Expression<Func<T, bool>> expression)
    {
        _predicate = _predicate is null ? expression : _predicate.AndAlso(expression);
        return this;
    }

    public PredicateBuilder<T> AndIf(bool condition, Expression<Func<T, bool>> expression) =>
        condition ? And(expression) : this;

    public PredicateBuilder<T> Or(Expression<Func<T, bool>> expression)
    {
        _predicate = _predicate is null ? expression : _predicate.OrElse(expression);
        return this;
    }

    public PredicateBuilder<T> OrIf(bool condition, Expression<Func<T, bool>> expression) =>
        condition ? Or(expression) : this;

    /// <summary>ANDs a bracketed OR group, e.g. (keyword in description OR keyword in reference).</summary>
    public PredicateBuilder<T> AndAnyOf(params Expression<Func<T, bool>>[] expressions)
    {
        if (expressions.Length == 0)
        {
            return this;
        }

        var group = expressions[0];
        for (var i = 1; i < expressions.Length; i++)
        {
            group = group.OrElse(expressions[i]);
        }

        return And(group);
    }

    public Expression<Func<T, bool>>? Build() => _predicate;

    public Expression<Func<T, bool>> BuildOrTrue() => _predicate ?? (_ => true);
}
