using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace FinanceAudit360.Infrastructure.Jobs;

/// <summary>Deletes stored PDFs older than the configured retention window; metadata is kept.</summary>
[DisallowConcurrentExecution]
public sealed class StatementRetentionJob(
    IApplicationDbContext context,
    IFileStorage fileStorage,
    ILogger<StatementRetentionJob> logger) : IJob
{
    public const string JobKey = "statement-retention";

    public async ValueTask Execute(IJobExecutionContext jobContext, CancellationToken cancellationToken)
    {
        var token = cancellationToken;

        var retentionDays = await context.Settings
            .AsNoTracking()
            .Where(s => s.Key == "Import.RetentionDays")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(token);

        if (!int.TryParse(retentionDays, out var days) || days <= 0)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);

        var expired = await context.StatementFiles
            .Where(f => f.CreatedOnUtc < cutoff && f.RelativePath != "")
            .Take(200)
            .ToListAsync(token);

        foreach (var file in expired)
        {
            try
            {
                await fileStorage.DeleteAsync(file.RelativePath, token);
                file.SetExtractedTextPath(null);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not delete expired statement file {FileName}.", file.StoredFileName);
            }
        }

        if (expired.Count > 0)
        {
            await context.SaveChangesAsync(token);
            logger.LogInformation("Retention job removed {Count} expired statement files.", expired.Count);
        }
    }
}

/// <summary>
/// Rebuilds card outstanding and account balances from the transaction history so the
/// dashboard totals cannot drift after manual edits.
/// </summary>
[DisallowConcurrentExecution]
public sealed class RecalculateBalancesJob(
    IApplicationDbContext context,
    ILogger<RecalculateBalancesJob> logger) : IJob
{
    public const string JobKey = "recalculate-balances";

    public async ValueTask Execute(IJobExecutionContext jobContext, CancellationToken cancellationToken)
    {
        var token = cancellationToken;

        var cardTotals = await context.Transactions
            .AsNoTracking()
            .Where(t => t.CreditCardId != null)
            .GroupBy(t => t.CreditCardId!.Value)
            .Select(g => new { CardId = g.Key, Net = g.Sum(t => t.DebitAmount) - g.Sum(t => t.CreditAmount) })
            .ToListAsync(token);

        var cards = await context.CreditCards.ToListAsync(token);
        foreach (var card in cards)
        {
            var total = cardTotals.FirstOrDefault(t => t.CardId == card.Id)?.Net ?? 0m;
            card.SyncOutstanding(total, DateTime.UtcNow);
        }

        var persons = await context.Persons.Include(p => p.LedgerEntries).ToListAsync(token);
        foreach (var person in persons)
        {
            person.Recalculate();
        }

        await context.SaveChangesAsync(token);
        logger.LogInformation("Recalculated {CardCount} cards and {PersonCount} person ledgers.", cards.Count, persons.Count);
    }
}

/// <summary>Purges audit rows beyond the retention window to keep the table manageable.</summary>
[DisallowConcurrentExecution]
public sealed class AuditLogCleanupJob(IApplicationDbContext context, ILogger<AuditLogCleanupJob> logger) : IJob
{
    public const string JobKey = "audit-log-cleanup";

    private const int RetentionDays = 400;

    public async ValueTask Execute(IJobExecutionContext jobContext, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        var deleted = await context.AuditLogs
            .Where(a => a.TimestampUtc < cutoff && a.Action != AuditAction.Login && a.Action != AuditAction.LoginFailed)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Removed {Count} audit log rows older than {Days} days.", deleted, RetentionDays);
        }
    }
}
