using FluentValidation;
using MediatR;
using ValidationException = FinanceAudit360.Application.Common.Exceptions.ValidationException;

namespace FinanceAudit360.Application.Common.Behaviours;

/// <summary>
/// Runs every FluentValidation validator registered for the request before the handler executes.
/// Minimal APIs do not run DataAnnotations, so this pipeline is the only validation gate.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var applicable = validators.ToArray();
        if (applicable.Length == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(applicable.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            var errors = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

            throw new ValidationException(errors);
        }

        return await next(cancellationToken);
    }
}
