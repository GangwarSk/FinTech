using System.Diagnostics;
using FinanceAudit360.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FinanceAudit360.Application.Common.Behaviours;

public sealed class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
    ICurrentUser currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling {RequestName} for user {UserName} (correlation {CorrelationId})",
            typeof(TRequest).Name,
            currentUser.UserName ?? "anonymous",
            currentUser.CorrelationId ?? "-");

        return await next(cancellationToken);
    }
}

/// <summary>Flags handlers that exceed the slow-request threshold so they can be tuned.</summary>
public sealed class PerformanceBehaviour<TRequest, TResponse>(
    ILogger<PerformanceBehaviour<TRequest, TResponse>> logger,
    ICurrentUser currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long SlowRequestThresholdMs = 800;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            logger.LogWarning(
                "Slow request {RequestName} took {ElapsedMilliseconds} ms for user {UserName}",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                currentUser.UserName ?? "anonymous");
        }

        return response;
    }
}

public sealed class UnhandledExceptionBehaviour<TRequest, TResponse>(
    ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception exception) when (exception is not Exceptions.ValidationException
                                              and not Exceptions.NotFoundException
                                              and not Exceptions.ConflictException
                                              and not Exceptions.ForbiddenAccessException
                                              and not Exceptions.UnauthorizedException)
        {
            logger.LogError(exception, "Unhandled exception while processing {RequestName}", typeof(TRequest).Name);
            throw;
        }
    }
}
