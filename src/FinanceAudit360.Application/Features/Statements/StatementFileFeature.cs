using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Contracts.Statements;
using FinanceAudit360.Contracts.Transactions;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Statements;

public sealed record SearchStatementFilesQuery(StatementFileFilterRequest Filter)
    : IRequest<PagedResult<StatementFileSummaryDto>>;

public sealed record GetStatementFileDetailQuery(Guid Id) : IRequest<StatementFileDetailDto>;

public sealed record GetStatementFileTransactionsQuery(Guid Id, StatementFileTransactionFilter Filter)
    : IRequest<PagedResult<TransactionDto>>;

public sealed record GetStatementFileImpactQuery(Guid Id) : IRequest<StatementFileImpactDto>;

public sealed record GetStatementFileLookupQuery(Guid? BankId, int? Year)
    : IRequest<IReadOnlyList<StatementFileLookupDto>>;

public sealed record DeleteStatementFileCommand(Guid Id) : IRequest<Unit>;

public sealed record RestoreStatementFileCommand(Guid Id) : IRequest<Unit>;

public sealed record PurgeStatementFileCommand(Guid Id) : IRequest<Unit>;

public sealed class SearchStatementFilesQueryHandler(IStatementFileRepository repository)
    : IRequestHandler<SearchStatementFilesQuery, PagedResult<StatementFileSummaryDto>>
{
    public async Task<PagedResult<StatementFileSummaryDto>> Handle(
        SearchStatementFilesQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.SearchAsync(request.Filter, cancellationToken);
        var items = page.Items.Select(p => p.ToSummaryDto()).ToList();

        return new PagedResult<StatementFileSummaryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

public sealed class GetStatementFileDetailQueryHandler(IStatementFileRepository repository)
    : IRequestHandler<GetStatementFileDetailQuery, StatementFileDetailDto>
{
    public async Task<StatementFileDetailDto> Handle(GetStatementFileDetailQuery request, CancellationToken cancellationToken)
    {
        var projection = await repository.GetProjectionAsync(request.Id, cancellationToken)
                         ?? throw new NotFoundException(nameof(StatementFile), request.Id);

        var statements = await repository.GetStatementsAsync(request.Id, cancellationToken);
        var uploads = await repository.GetUploadsAsync(request.Id, cancellationToken);

        return new StatementFileDetailDto(
            projection.ToSummaryDto(),
            projection.File.IsPasswordProtected,
            projection.File.PageCount,
            projection.File.ContentHash,
            statements,
            uploads);
    }
}

public sealed class GetStatementFileTransactionsQueryHandler(IStatementFileRepository repository)
    : IRequestHandler<GetStatementFileTransactionsQuery, PagedResult<TransactionDto>>
{
    public Task<PagedResult<TransactionDto>> Handle(
        GetStatementFileTransactionsQuery request,
        CancellationToken cancellationToken) =>
        repository.GetTransactionsAsync(request.Id, request.Filter, cancellationToken);
}

public sealed class GetStatementFileImpactQueryHandler(IStatementFileRepository repository)
    : IRequestHandler<GetStatementFileImpactQuery, StatementFileImpactDto>
{
    public async Task<StatementFileImpactDto> Handle(GetStatementFileImpactQuery request, CancellationToken cancellationToken)
    {
        var projection = await repository.GetProjectionAsync(request.Id, cancellationToken)
                         ?? throw new NotFoundException(nameof(StatementFile), request.Id);

        return new StatementFileImpactDto(
            projection.File.Id,
            projection.File.OriginalFileName,
            projection.BuildLabel(),
            projection.StatementCount,
            projection.TransactionCount,
            projection.UploadCount,
            projection.File.IsDeleted);
    }
}

public sealed class GetStatementFileLookupQueryHandler(IStatementFileRepository repository)
    : IRequestHandler<GetStatementFileLookupQuery, IReadOnlyList<StatementFileLookupDto>>
{
    public Task<IReadOnlyList<StatementFileLookupDto>> Handle(
        GetStatementFileLookupQuery request,
        CancellationToken cancellationToken) =>
        repository.GetLookupAsync(request.BankId, request.Year, cancellationToken);
}

/// <summary>
/// Soft-deletes an uploaded file and everything it produced - upload history, statement and every imported
/// transaction - in one transaction, so the chain is never half-deleted. The PDF stays on disk until the
/// file is purged from the recycle bin.
/// </summary>
public sealed class DeleteStatementFileCommandHandler(
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : IRequestHandler<DeleteStatementFileCommand, Unit>
{
    public async Task<Unit> Handle(DeleteStatementFileCommand command, CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var file = await context.StatementFiles
                           .IgnoreQueryFilters()
                           .FirstOrDefaultAsync(f => f.Id == command.Id, token)
                       ?? throw new NotFoundException(nameof(StatementFile), command.Id);

            if (file.IsDeleted)
            {
                throw new ConflictException(
                    "statementfile.already_deleted",
                    "This file is already in the recycle bin.");
            }

            var user = currentUser.UserName;
            var now = dateTime.UtcNow;

            var statements = await context.Statements
                .IgnoreQueryFilters()
                .Where(s => s.StatementFileId == command.Id && !s.IsDeleted)
                .ToListAsync(token);

            var statementIds = statements.Select(s => s.Id).ToList();

            var transactions = await context.Transactions
                .IgnoreQueryFilters()
                .Where(t => t.StatementId != null && statementIds.Contains(t.StatementId.Value) && !t.IsDeleted)
                .ToListAsync(token);

            var uploads = await context.UploadHistories
                .IgnoreQueryFilters()
                .Where(u => u.StatementFileId == command.Id && !u.IsDeleted)
                .ToListAsync(token);

            foreach (var transaction in transactions)
            {
                transaction.MarkDeleted(user, now);
            }

            foreach (var statement in statements)
            {
                statement.MarkDeleted(user, now);
            }

            foreach (var upload in uploads)
            {
                upload.MarkDeleted(user, now);
            }

            file.MarkDeleted(user, now);

            await unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Brings a file back from the recycle bin. Both the statement dedupe key and the transaction dedupe hash
/// are unique only among live rows, so a newer import can legitimately have claimed them while this file was
/// deleted. Those cases are reported as a conflict instead of surfacing as a database constraint error.
/// </summary>
public sealed class RestoreStatementFileCommandHandler(
    IApplicationDbContext context,
    IUnitOfWork unitOfWork) : IRequestHandler<RestoreStatementFileCommand, Unit>
{
    public async Task<Unit> Handle(RestoreStatementFileCommand command, CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var file = await context.StatementFiles
                           .IgnoreQueryFilters()
                           .FirstOrDefaultAsync(f => f.Id == command.Id, token)
                       ?? throw new NotFoundException(nameof(StatementFile), command.Id);

            if (!file.IsDeleted)
            {
                throw new ConflictException("statementfile.not_deleted", "This file is not in the recycle bin.");
            }

            var statements = await context.Statements
                .IgnoreQueryFilters()
                .Where(s => s.StatementFileId == command.Id && s.IsDeleted)
                .ToListAsync(token);

            var statementIds = statements.Select(s => s.Id).ToList();

            var transactions = await context.Transactions
                .IgnoreQueryFilters()
                .Where(t => t.StatementId != null && statementIds.Contains(t.StatementId.Value) && t.IsDeleted)
                .ToListAsync(token);

            await GuardAgainstConflictsAsync(statements, transactions, token);

            var uploads = await context.UploadHistories
                .IgnoreQueryFilters()
                .Where(u => u.StatementFileId == command.Id && u.IsDeleted)
                .ToListAsync(token);

            file.Restore();

            foreach (var upload in uploads)
            {
                upload.Restore();
            }

            foreach (var statement in statements)
            {
                statement.Restore();
            }

            foreach (var transaction in transactions)
            {
                transaction.Restore();
            }

            await unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return Unit.Value;
    }

    private async Task GuardAgainstConflictsAsync(
        IReadOnlyList<CreditCardStatement> statements,
        IReadOnlyList<Transaction> transactions,
        CancellationToken cancellationToken)
    {
        var dedupeKeys = statements.Select(s => s.DedupeKey).ToList();

        var conflictingStatement = await context.Statements
            .IgnoreQueryFilters()
            .Where(s => !s.IsDeleted && dedupeKeys.Contains(s.DedupeKey))
            .Select(s => new { s.PeriodStart, s.PeriodEnd })
            .FirstOrDefaultAsync(cancellationToken);

        if (conflictingStatement is not null)
        {
            throw new ConflictException(
                "statementfile.restore_conflict",
                $"This file cannot be restored: the statement for {conflictingStatement.PeriodStart:dd MMM yyyy} - " +
                $"{conflictingStatement.PeriodEnd:dd MMM yyyy} has already been imported again from another file. " +
                "Delete that import first, or permanently delete this file instead.");
        }

        var dedupeHashes = transactions.Select(t => t.DedupeHash).ToList();

        var hasConflictingTransaction = await context.Transactions
            .IgnoreQueryFilters()
            .AnyAsync(t => !t.IsDeleted && t.StatementId != null && dedupeHashes.Contains(t.DedupeHash), cancellationToken);

        if (hasConflictingTransaction)
        {
            throw new ConflictException(
                "statementfile.restore_conflict",
                "This file cannot be restored: some of its transactions have already been imported again from " +
                "another file. Delete that import first, or permanently delete this file instead.");
        }
    }
}

/// <summary>
/// Permanently removes a recycled file from every table and deletes the stored PDF. Storage is cleared
/// first: if that fails the recycle-bin entry is still there to retry, whereas the reverse would orphan
/// the file on disk with no record pointing at it.
/// </summary>
public sealed class PurgeStatementFileCommandHandler(
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    IFileStorage fileStorage) : IRequestHandler<PurgeStatementFileCommand, Unit>
{
    public async Task<Unit> Handle(PurgeStatementFileCommand command, CancellationToken cancellationToken)
    {
        var file = await context.StatementFiles
                       .IgnoreQueryFilters()
                       .AsNoTracking()
                       .FirstOrDefaultAsync(f => f.Id == command.Id, cancellationToken)
                   ?? throw new NotFoundException(nameof(StatementFile), command.Id);

        if (!file.IsDeleted)
        {
            throw new ConflictException(
                "statementfile.not_deleted",
                "Only files in the recycle bin can be permanently deleted. Delete the file first.");
        }

        await fileStorage.DeleteAsync(file.RelativePath, cancellationToken);

        if (!string.IsNullOrWhiteSpace(file.ExtractedTextPath))
        {
            await fileStorage.DeleteAsync(file.ExtractedTextPath, cancellationToken);
        }

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var statementIds = await context.Statements
                .IgnoreQueryFilters()
                .Where(s => s.StatementFileId == command.Id)
                .Select(s => s.Id)
                .ToListAsync(token);

            // ExecuteDelete issues raw DELETE statements, bypassing the interceptor that would otherwise
            // turn these into soft deletes. Order follows the foreign keys: transactions reference
            // statements with Restrict, so they have to go first. The database's ON DELETE SET NULL clears
            // any MoneyLedger rows still pointing at these transactions.
            await context.Transactions
                .IgnoreQueryFilters()
                .Where(t => t.StatementId != null && statementIds.Contains(t.StatementId.Value))
                .ExecuteDeleteAsync(token);

            await context.UploadHistories
                .IgnoreQueryFilters()
                .Where(u => u.StatementFileId == command.Id)
                .ExecuteDeleteAsync(token);

            await context.Statements
                .IgnoreQueryFilters()
                .Where(s => s.StatementFileId == command.Id)
                .ExecuteDeleteAsync(token);

            await context.StatementFiles
                .IgnoreQueryFilters()
                .Where(f => f.Id == command.Id)
                .ExecuteDeleteAsync(token);
        }, cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteStatementFileCommandValidator : AbstractValidator<DeleteStatementFileCommand>
{
    public DeleteStatementFileCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class RestoreStatementFileCommandValidator : AbstractValidator<RestoreStatementFileCommand>
{
    public RestoreStatementFileCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class PurgeStatementFileCommandValidator : AbstractValidator<PurgeStatementFileCommand>
{
    public PurgeStatementFileCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

internal static class StatementFileProjectionExtensions
{
    public static string BuildLabel(this StatementFileProjection projection) => StatementFileLabel.Build(
        projection.BankName,
        projection.File.OriginalFileName,
        projection.PeriodStart,
        projection.PeriodEnd,
        projection.UploadedOnUtc);

    public static StatementFileSummaryDto ToSummaryDto(this StatementFileProjection projection) => new(
        projection.File.Id,
        projection.File.OriginalFileName,
        projection.BuildLabel(),
        projection.File.SizeInBytes,
        projection.BankId,
        projection.BankName,
        projection.File.DetectedBank == Domain.Enums.BankCode.Unknown ? null : projection.File.DetectedBank.ToString(),
        projection.File.DetectedKind == Domain.Enums.StatementKind.Unknown ? null : projection.File.DetectedKind.ToString(),
        projection.PeriodStart,
        projection.PeriodEnd,
        projection.StatementDate,
        projection.StatementCount,
        projection.TransactionCount,
        projection.UploadCount,
        projection.UploadStatus,
        projection.UploadedOnUtc,
        projection.UploadedBy,
        projection.File.IsDeleted,
        projection.File.DeletedOnUtc,
        projection.File.DeletedBy);
}
