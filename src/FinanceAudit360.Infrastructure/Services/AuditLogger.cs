using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FinanceAudit360.Infrastructure.Services;

public sealed class AuditLogger(
    IAuditLogRepository repository,
    ICurrentUser currentUser,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    public async Task LogAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        try
        {
            log.WithActor(currentUser.UserId, currentUser.UserName, currentUser.IpAddress, currentUser.UserAgent, currentUser.CorrelationId);
            await repository.AddAsync(log, cancellationToken);
        }
        catch (Exception exception)
        {
            // Auditing must never break the request it is recording.
            logger.LogError(exception, "Failed to persist audit log for {EntityName}.", log.EntityName);
        }
    }

    public Task LogSecurityAsync(AuditAction action, string message, CancellationToken cancellationToken = default) =>
        LogAsync(AuditLog.ForSecurity(action, message), cancellationToken);
}
