using System.Net;
using System.Text.Json;
using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Api.Middleware;

/// <summary>
/// Converts every unhandled exception into the standard <see cref="ApiResponse{T}"/> envelope.
/// Internal details are logged but never returned to the caller outside development.
/// </summary>
public sealed class GlobalExceptionHandler(
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, code, message, validationErrors) = Map(exception);

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("{Code}: {Message} on {Method} {Path}.", code, message, httpContext.Request.Method, httpContext.Request.Path);
        }

        if (statusCode >= 500 && !environment.IsDevelopment())
        {
            message = "An unexpected error occurred. Please try again or contact support.";
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var payload = ApiResponse<object>.Fail(new ApiError(code, message, validationErrors));

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(payload, JsonOptions),
            cancellationToken);

        return true;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static (int StatusCode, string Code, string Message, IReadOnlyDictionary<string, string[]>? Errors) Map(Exception exception) =>
        exception switch
        {
            ValidationException validation => (
                (int)HttpStatusCode.BadRequest,
                "validation_failed",
                validation.Message,
                validation.Errors),

            NotFoundException notFound => ((int)HttpStatusCode.NotFound, "not_found", notFound.Message, null),

            ConflictException conflict => ((int)HttpStatusCode.Conflict, conflict.Code, conflict.Message, null),

            UnauthorizedException unauthorized => ((int)HttpStatusCode.Unauthorized, "unauthorized", unauthorized.Message, null),

            ForbiddenAccessException forbidden => ((int)HttpStatusCode.Forbidden, "forbidden", forbidden.Message, null),

            EntityNotFoundException entityNotFound => ((int)HttpStatusCode.NotFound, entityNotFound.Code, entityNotFound.Message, null),

            BusinessRuleViolationException rule => ((int)HttpStatusCode.UnprocessableEntity, rule.Code, rule.Message, null),

            DomainException domain => ((int)HttpStatusCode.BadRequest, domain.Code, domain.Message, null),

            DbUpdateConcurrencyException => (
                (int)HttpStatusCode.Conflict,
                "concurrency_conflict",
                "The record was modified by someone else. Reload and try again.",
                null),

            DbUpdateException => (
                (int)HttpStatusCode.Conflict,
                "database_conflict",
                "The operation violates a database constraint.",
                null),

            OperationCanceledException => (499, "request_cancelled", "The request was cancelled.", null),

            _ => ((int)HttpStatusCode.InternalServerError, "server_error", exception.Message, null)
        };
}
