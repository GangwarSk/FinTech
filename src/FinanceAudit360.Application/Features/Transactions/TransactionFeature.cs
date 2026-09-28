using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Transactions;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Constants;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Transactions;

public sealed record SearchTransactionsQuery(TransactionFilterRequest Filter) : IRequest<TransactionSearchResultDto>;

public sealed record GetTransactionByIdQuery(Guid Id) : IRequest<TransactionDto>;

public sealed record CreateTransactionCommand(CreateTransactionRequest Request) : IRequest<IdResponse>;

public sealed record UpdateTransactionCommand(Guid Id, UpdateTransactionRequest Request) : IRequest<Unit>;

public sealed record DeleteTransactionCommand(Guid Id) : IRequest<Unit>;

public sealed record BulkAssignTransactionsCommand(BulkAssignRequest Request) : IRequest<BulkResultDto>;

public sealed record ExportTransactionsQuery(TransactionFilterRequest Filter) : IRequest<byte[]>;

public sealed class SearchTransactionsQueryHandler(ITransactionRepository repository)
    : IRequestHandler<SearchTransactionsQuery, TransactionSearchResultDto>
{
    public Task<TransactionSearchResultDto> Handle(SearchTransactionsQuery request, CancellationToken cancellationToken) =>
        repository.SearchAsync(request.Filter, cancellationToken);
}

public sealed class GetTransactionByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTransactionByIdQuery, TransactionDto>
{
    public async Task<TransactionDto> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await context.Transactions
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(ProjectionMappings.TransactionProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException(nameof(Transaction), request.Id);
    }
}

public sealed class CreateTransactionCommandHandler(
    ITransactionRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateTransactionCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var transaction = Transaction.Create(
            request.TransactionDate,
            request.Amount,
            (TransactionDirection)request.Direction,
            (TransactionType)request.TransactionType,
            request.Description,
            request.Currency ?? AppConstants.DefaultCurrency);

        transaction.SetPostingDate(request.PostingDate ?? request.TransactionDate);
        transaction.SetSource(TransactionSource.Manual);
        transaction.SetInstrument(request.BankId, request.BankAccountId, request.CreditCardId);
        transaction.SetReference(request.ReferenceNumber, null, null);
        transaction.AssignVendor(request.VendorId);
        transaction.AssignCategory(request.CategoryId);
        transaction.AssignPerson(request.PersonId);

        await repository.AddAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(transaction.Id);
    }
}

public sealed class UpdateTransactionCommandHandler(
    ITransactionRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateTransactionCommand, Unit>
{
    public async Task<Unit> Handle(UpdateTransactionCommand command, CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                          ?? throw new NotFoundException(nameof(Transaction), command.Id);

        var request = command.Request;

        transaction.UpdateDetails(
            request.TransactionDate,
            request.PostingDate ?? request.TransactionDate,
            request.Amount,
            (TransactionDirection)request.Direction,
            (TransactionType)request.TransactionType,
            request.Description,
            request.Notes);

        transaction.SetInstrument(request.BankId, request.BankAccountId, request.CreditCardId);
        transaction.SetReference(request.ReferenceNumber, transaction.MerchantRawText, transaction.Location);
        transaction.AssignVendor(request.VendorId);
        transaction.AssignCategory(request.CategoryId);
        transaction.AssignPerson(request.PersonId);
        transaction.MarkReconciled(request.IsReconciled);
        transaction.MarkRecurring(request.IsRecurring);
        transaction.MarkDisputed(request.IsDisputed);

        repository.Update(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteTransactionCommandHandler(
    ITransactionRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeleteTransactionCommand, Unit>
{
    public async Task<Unit> Handle(DeleteTransactionCommand command, CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                          ?? throw new NotFoundException(nameof(Transaction), command.Id);

        repository.SoftDelete(transaction, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class BulkAssignTransactionsCommandHandler(
    ITransactionRepository repository,
    ICurrentUser currentUser) : IRequestHandler<BulkAssignTransactionsCommand, BulkResultDto>
{
    public async Task<BulkResultDto> Handle(BulkAssignTransactionsCommand command, CancellationToken cancellationToken)
    {
        var affected = await repository.BulkAssignAsync(command.Request, currentUser.UserName, cancellationToken);
        var requested = command.Request.TransactionIds.Count;

        return new BulkResultDto(requested, affected, requested - affected, []);
    }
}

/// <summary>Streams the filtered result set as CSV. Paging is widened to the export cap.</summary>
public sealed class ExportTransactionsQueryHandler(ITransactionRepository repository)
    : IRequestHandler<ExportTransactionsQuery, byte[]>
{
    private const int ExportPageSize = 500;
    private const int MaxExportRows = 50_000;

    public async Task<byte[]> Handle(ExportTransactionsQuery request, CancellationToken cancellationToken)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(
            "TransactionDate,PostingDate,Description,ReferenceNumber,Direction,TransactionType,Amount,CreditAmount,DebitAmount,Currency,Bank,Card,Account,Vendor,Person,Category,Statement");

        var filter = request.Filter;
        filter.PageSize = ExportPageSize;
        filter.PageNumber = 1;

        var exported = 0;
        while (exported < MaxExportRows)
        {
            var page = await repository.SearchAsync(filter, cancellationToken);
            if (page.Items.Count == 0)
            {
                break;
            }

            foreach (var item in page.Items)
            {
                builder.AppendLine(string.Join(',',
                    Csv(item.TransactionDate.ToString("yyyy-MM-dd")),
                    Csv(item.PostingDate.ToString("yyyy-MM-dd")),
                    Csv(item.Description),
                    Csv(item.ReferenceNumber),
                    Csv(item.Direction),
                    Csv(item.TransactionType),
                    Csv(item.Amount.ToString("F2")),
                    Csv(item.CreditAmount.ToString("F2")),
                    Csv(item.DebitAmount.ToString("F2")),
                    Csv(item.Currency),
                    Csv(item.BankName),
                    Csv(item.CreditCardName),
                    Csv(item.BankAccountName),
                    Csv(item.VendorName),
                    Csv(item.PersonName),
                    Csv(item.CategoryName),
                    Csv(item.StatementPeriod)));
            }

            exported += page.Items.Count;
            if (filter.PageNumber >= page.TotalPages)
            {
                break;
            }

            filter.PageNumber++;
        }

        return System.Text.Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>
    /// Quotes the field and neutralises leading =, +, - and @ so spreadsheet applications do not
    /// evaluate exported text as a formula (CSV injection).
    /// </summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value.Length > 0 && (value[0] is '=' or '+' or '-' or '@')
            ? "'" + value
            : value;

        return $"\"{sanitized.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
