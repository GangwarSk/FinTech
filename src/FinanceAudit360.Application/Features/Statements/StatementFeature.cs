using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Statements;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Statements;

public sealed record UploadStatementCommand(Stream Content, string FileName, long SizeInBytes)
    : IRequest<UploadStatementResponse>;

public sealed record ProcessStatementCommand(ProcessStatementRequest Request) : IRequest<StatementProcessingResultDto>;

public sealed record SearchStatementsQuery(StatementFilterRequest Filter) : IRequest<PagedResult<StatementDto>>;

public sealed record GetStatementByIdQuery(Guid Id) : IRequest<StatementDto>;

public sealed record GetUploadHistoryQuery(UploadHistoryFilterRequest Filter) : IRequest<PagedResult<UploadHistoryDto>>;

public sealed record DeleteStatementCommand(Guid Id) : IRequest<Unit>;

public sealed class UploadStatementCommandHandler(IStatementImportService importService)
    : IRequestHandler<UploadStatementCommand, UploadStatementResponse>
{
    public Task<UploadStatementResponse> Handle(UploadStatementCommand request, CancellationToken cancellationToken) =>
        importService.UploadAsync(request.Content, request.FileName, request.SizeInBytes, cancellationToken);
}

public sealed class ProcessStatementCommandHandler(IStatementImportService importService)
    : IRequestHandler<ProcessStatementCommand, StatementProcessingResultDto>
{
    public Task<StatementProcessingResultDto> Handle(ProcessStatementCommand request, CancellationToken cancellationToken) =>
        importService.ProcessAsync(request.Request, cancellationToken);
}

public sealed class SearchStatementsQueryHandler(IStatementRepository repository)
    : IRequestHandler<SearchStatementsQuery, PagedResult<StatementDto>>
{
    public async Task<PagedResult<StatementDto>> Handle(SearchStatementsQuery request, CancellationToken cancellationToken)
    {
        var page = await repository.SearchAsync(request.Filter, cancellationToken);
        var items = page.Items
            .Select(p => p.Statement.ToDto(p.BankName, p.OriginalFileName))
            .ToList();

        return new PagedResult<StatementDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

public sealed class GetStatementByIdQueryHandler(IStatementRepository repository, IApplicationDbContext context)
    : IRequestHandler<GetStatementByIdQuery, StatementDto>
{
    public async Task<StatementDto> Handle(GetStatementByIdQuery request, CancellationToken cancellationToken)
    {
        var statement = await repository.GetDetailAsync(request.Id, cancellationToken)
                        ?? throw new NotFoundException(nameof(CreditCardStatement), request.Id);

        var bankName = await context.Banks
            .Where(b => b.Id == statement.BankId)
            .Select(b => b.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        var fileName = statement.StatementFileId is null
            ? null
            : await context.StatementFiles
                .Where(f => f.Id == statement.StatementFileId)
                .Select(f => f.OriginalFileName)
                .FirstOrDefaultAsync(cancellationToken);

        return statement.ToDto(bankName, fileName);
    }
}

public sealed class GetUploadHistoryQueryHandler(IUploadHistoryRepository repository)
    : IRequestHandler<GetUploadHistoryQuery, PagedResult<UploadHistoryDto>>
{
    public async Task<PagedResult<UploadHistoryDto>> Handle(GetUploadHistoryQuery request, CancellationToken cancellationToken)
    {
        var page = await repository.SearchAsync(request.Filter, cancellationToken);
        var items = page.Items.Select(h => h.ToDto()).ToList();

        return new PagedResult<UploadHistoryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

/// <summary>
/// Soft-deletes the statement and every transaction imported from it so the audit trail
/// stays consistent - a statement is never removed while its lines remain searchable.
/// </summary>
public sealed class DeleteStatementCommandHandler(
    IStatementRepository repository,
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : IRequestHandler<DeleteStatementCommand, Unit>
{
    public async Task<Unit> Handle(DeleteStatementCommand command, CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var statement = await repository.GetByIdAsync(command.Id, asNoTracking: false, token)
                            ?? throw new NotFoundException(nameof(CreditCardStatement), command.Id);

            var transactions = await context.Transactions
                .Where(t => t.StatementId == command.Id)
                .ToListAsync(token);

            foreach (var transaction in transactions)
            {
                transaction.MarkDeleted(currentUser.UserName, dateTime.UtcNow);
            }

            repository.SoftDelete(statement, currentUser.UserName);
            await unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return Unit.Value;
    }
}

public sealed class UploadStatementCommandValidator : AbstractValidator<UploadStatementCommand>
{
    private static readonly string[] AllowedExtensions = [".pdf"];

    public UploadStatementCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(260)
            .Must(name => AllowedExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
            .WithMessage("Only PDF statements can be uploaded.")
            .Must(name => name == Path.GetFileName(name))
            .WithMessage("The file name must not contain a path.");

        RuleFor(x => x.SizeInBytes)
            .GreaterThan(0).WithMessage("The uploaded file is empty.")
            .LessThanOrEqualTo(AppConstants.MaxUploadBytes)
            .WithMessage($"The file exceeds the {AppConstants.MaxUploadBytes / (1024 * 1024)} MB limit.");
    }
}

public sealed class ProcessStatementCommandValidator : AbstractValidator<ProcessStatementCommand>
{
    public ProcessStatementCommandValidator()
    {
        RuleFor(x => x.Request.UploadId).NotEmpty();
        RuleFor(x => x.Request.Password).MaximumLength(256);
    }
}

public sealed class DeleteStatementCommandValidator : AbstractValidator<DeleteStatementCommand>
{
    public DeleteStatementCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
