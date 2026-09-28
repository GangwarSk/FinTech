using System.Diagnostics;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Infrastructure.Options;
using FinanceAudit360.Shared.Constants;
using Microsoft.Extensions.Options;

namespace FinanceAudit360.Api.Middleware;

/// <summary>Assigns a correlation id to every request and echoes it back for client-side tracing.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(AppConstants.CorrelationIdHeader, out var supplied)
                            && !string.IsNullOrWhiteSpace(supplied)
            ? supplied.ToString()
            : Guid.CreateVersion7().ToString("N");

        context.Items[AppConstants.CorrelationIdHeader] = correlationId;
        context.Response.Headers[AppConstants.CorrelationIdHeader] = correlationId;

        await next(context);
    }
}

/// <summary>
/// Adds the standard hardening headers. The API returns JSON only, so the CSP is locked to
/// 'none' - the SPA serves its own policy. The Swagger UI is the one exception: it is a real
/// HTML page with inline bootstrap script and styles, so it gets a narrowly relaxed policy.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; sandbox";

    private const string SwaggerContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-XSS-Protection"] = "0";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        headers["Content-Security-Policy"] = IsSwaggerRequest(context.Request)
            ? SwaggerContentSecurityPolicy
            : ApiContentSecurityPolicy;
        headers.Remove("Server");

        await next(context);
    }

    private static bool IsSwaggerRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Writes one audit row per mutating request so every change has a traceable origin.</summary>
public sealed class RequestAuditMiddleware(RequestDelegate next, IOptions<SecurityOptions> securityOptions)
{
    private static readonly string[] MutatingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    public async Task InvokeAsync(HttpContext context, IAuditLogger auditLogger)
    {
        if (!securityOptions.Value.EnableRequestAuditLogging || !MutatingMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var log = AuditLog.ForRequest(
                context.Request.Path.Value ?? string.Empty,
                context.Request.Method,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.Items[AppConstants.CorrelationIdHeader] as string);

            await auditLogger.LogAsync(log, context.RequestAborted);
        }
    }
}
