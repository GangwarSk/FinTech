using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Extensions;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Masters.Accounts;

public sealed record SearchBankAccountsQuery(MasterFilterRequest Filter) : IRequest<PagedResult<BankAccountDto>>;

public sealed record GetBankAccountByIdQuery(Guid Id) : IRequest<BankAccountDto>;

public sealed record GetBankAccountLookupQuery : IRequest<IReadOnlyList<LookupDto>>;

public sealed record CreateBankAccountCommand(SaveBankAccountRequest Request) : IRequest<IdResponse>;

public sealed record UpdateBankAccountCommand(Guid Id, SaveBankAccountRequest Request) : IRequest<Unit>;

public sealed record DeleteBankAccountCommand(Guid Id) : IRequest<Unit>;

public sealed class SearchBankAccountsQueryHandler(IBankAccountRepository repository)
    : IRequestHandler<SearchBankAccountsQuery, PagedResult<BankAccountDto>>
{
    public async Task<PagedResult<BankAccountDto>> Handle(SearchBankAccountsQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var page = await repository.SearchAsync(filter.Keyword, filter.BankId, filter.IsActive, filter.PageNumber, filter.PageSize, cancellationToken);
        var items = page.Items.Select(p => p.Account.ToDto(p.BankName, p.TransactionCount)).ToList();

        return new PagedResult<BankAccountDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

public sealed class GetBankAccountByIdQueryHandler(IBankAccountRepository repository, IApplicationDbContext context)
    : IRequestHandler<GetBankAccountByIdQuery, BankAccountDto>
{
    public async Task<BankAccountDto> Handle(GetBankAccountByIdQuery request, CancellationToken cancellationToken)
    {
        var account = await repository.GetWithBankAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException(nameof(BankAccount), request.Id);

        var transactionCount = await context.Transactions.CountAsync(t => t.BankAccountId == request.Id, cancellationToken);
        return account.ToDto(account.Bank?.Name ?? "Unknown", transactionCount);
    }
}

public sealed class GetBankAccountLookupQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetBankAccountLookupQuery, IReadOnlyList<LookupDto>>
{
    public async Task<IReadOnlyList<LookupDto>> Handle(GetBankAccountLookupQuery request, CancellationToken cancellationToken) =>
        await context.BankAccounts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Nickname)
            .Select(a => new LookupDto(
                a.Id,
                a.Nickname ?? a.Bank!.ShortName + " ****" + a.AccountNumberLast4,
                a.Bank!.Name))
            .ToListAsync(cancellationToken);
}

public sealed class CreateBankAccountCommandHandler(
    IBankAccountRepository repository,
    IBankRepository bankRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateBankAccountCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateBankAccountCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        _ = await bankRepository.GetByIdAsync(request.BankId, cancellationToken)
            ?? throw new NotFoundException(nameof(Bank), request.BankId);

        var last4 = request.AccountNumber.Last4();
        if (await repository.AnyAsync(a => a.BankId == request.BankId && a.AccountNumberLast4 == last4, cancellationToken))
        {
            throw new ConflictException("account.duplicate", "An account ending with these digits already exists for this bank.");
        }

        var account = BankAccount.Create(
            request.BankId,
            request.AccountNumber,
            request.AccountHolderName,
            (AccountType)request.AccountType,
            request.Nickname,
            request.Ifsc,
            request.BranchName,
            request.Currency ?? "INR");

        account.Update(request.AccountHolderName, (AccountType)request.AccountType, request.Nickname, request.Ifsc, request.BranchName, request.OpenedOn, request.Notes);
        account.SetActive(request.IsActive);
        account.SetPrimary(request.IsPrimary);

        await repository.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(account.Id);
    }
}

public sealed class UpdateBankAccountCommandHandler(IBankAccountRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateBankAccountCommand, Unit>
{
    public async Task<Unit> Handle(UpdateBankAccountCommand command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                      ?? throw new NotFoundException(nameof(BankAccount), command.Id);

        var request = command.Request;
        account.Update(request.AccountHolderName, (AccountType)request.AccountType, request.Nickname, request.Ifsc, request.BranchName, request.OpenedOn, request.Notes);
        account.SetActive(request.IsActive);
        account.SetPrimary(request.IsPrimary);

        repository.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteBankAccountCommandHandler(
    IBankAccountRepository repository,
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeleteBankAccountCommand, Unit>
{
    public async Task<Unit> Handle(DeleteBankAccountCommand command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                      ?? throw new NotFoundException(nameof(BankAccount), command.Id);

        if (await context.Transactions.AnyAsync(t => t.BankAccountId == command.Id, cancellationToken))
        {
            throw new ConflictException("account.in_use", "This account has transactions and cannot be deleted.");
        }

        repository.SoftDelete(account, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class SaveBankAccountRequestValidator : AbstractValidator<SaveBankAccountRequest>
{
    public SaveBankAccountRequestValidator()
    {
        RuleFor(x => x.BankId).NotEmpty();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .Must(n => n.DigitsOnly().Length >= 4)
            .WithMessage("The account number must contain at least 4 digits.");
        RuleFor(x => x.AccountHolderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountType).Must(v => Enum.IsDefined(typeof(AccountType), v)).WithMessage("Unknown account type.");
        RuleFor(x => x.Nickname).MaximumLength(100);
        RuleFor(x => x.Ifsc).MaximumLength(20);
        RuleFor(x => x.BranchName).MaximumLength(200);
        RuleFor(x => x.Currency).Length(3).When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateBankAccountCommandValidator : AbstractValidator<CreateBankAccountCommand>
{
    public CreateBankAccountCommandValidator() => RuleFor(x => x.Request).NotNull().SetValidator(new SaveBankAccountRequestValidator());
}

public sealed class UpdateBankAccountCommandValidator : AbstractValidator<UpdateBankAccountCommand>
{
    public UpdateBankAccountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new SaveBankAccountRequestValidator());
    }
}

public sealed class DeleteBankAccountCommandValidator : AbstractValidator<DeleteBankAccountCommand>
{
    public DeleteBankAccountCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
