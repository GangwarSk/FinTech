using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

/// <summary>Append-only security/change trail. Rows are never updated or soft-deleted.</summary>
public class AuditLog : Entity, IAggregateRoot
{
    private AuditLog()
    {
    }

    private AuditLog(AuditAction action, string entityName)
    {
        Action = action;
        EntityName = entityName;
        TimestampUtc = DateTime.UtcNow;
    }

    public AuditAction Action { get; private set; }

    public string EntityName { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public Guid? UserId { get; private set; }

    public string? UserName { get; private set; }

    public DateTime TimestampUtc { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? HttpMethod { get; private set; }

    public string? Endpoint { get; private set; }

    public int? StatusCode { get; private set; }

    public long? DurationMs { get; private set; }

    public string? OldValuesJson { get; private set; }

    public string? NewValuesJson { get; private set; }

    public string? AffectedColumns { get; private set; }

    public string? Message { get; private set; }

    public static AuditLog ForEntity(
        AuditAction action,
        string entityName,
        string? entityId,
        string? oldValuesJson,
        string? newValuesJson,
        string? affectedColumns) =>
        new(action, entityName)
        {
            EntityId = entityId,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            AffectedColumns = affectedColumns
        };

    public static AuditLog ForSecurity(AuditAction action, string? message) =>
        new(action, "Security") { Message = message.NormalizeOrNull()?.Truncate(1000) };

    public static AuditLog ForRequest(
        string endpoint,
        string httpMethod,
        int statusCode,
        long durationMs,
        string? correlationId) =>
        new(AuditAction.Update, "HttpRequest")
        {
            Endpoint = endpoint.Truncate(500),
            HttpMethod = httpMethod,
            StatusCode = statusCode,
            DurationMs = durationMs,
            CorrelationId = correlationId
        };

    public AuditLog WithActor(Guid? userId, string? userName, string? ipAddress, string? userAgent, string? correlationId)
    {
        UserId = userId;
        UserName = userName.NormalizeOrNull();
        IpAddress = ipAddress.NormalizeOrNull();
        UserAgent = userAgent.NormalizeOrNull()?.Truncate(500);
        CorrelationId = correlationId.NormalizeOrNull();
        return this;
    }
}
