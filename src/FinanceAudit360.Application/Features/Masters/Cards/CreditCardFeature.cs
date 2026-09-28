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

namespace FinanceAudit360.Application.Features.Masters.Cards;

public sealed record SearchCreditCardsQuery(MasterFilterRequest Filter) : IRequest<PagedResult<CreditCardDto>>;

public sealed record GetCreditCardByIdQuery(Guid Id) : IRequest<CreditCardDto>;

public sealed record GetCreditCardLookupQuery : IRequest<IReadOnlyList<LookupDto>>;

public sealed record CreateCreditCardCommand(SaveCreditCardRequest Request) : IRequest<IdResponse>;

public sealed record UpdateCreditCardCommand(Guid Id, SaveCreditCardRequest Request) : IRequest<Unit>;

public sealed record DeleteCreditCardCommand(Guid Id) : IRequest<Unit>;

public sealed class SearchCreditCardsQueryHandler(ICreditCardRepository repository)
    : IRequestHandler<SearchCreditCardsQuery, PagedResult<CreditCardDto>>
{
    public async Task<PagedResult<CreditCardDto>> Handle(SearchCreditCardsQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var page = await repository.SearchAsync(filter.Keyword, filter.BankId, filter.IsActive, filter.PageNumber, filter.PageSize, cancellationToken);
        var items = page.Items.Select(p => p.Card.ToDto(p.BankName, p.TransactionCount, p.StatementCount)).ToList();

        return new PagedResult<CreditCardDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

public sealed class GetCreditCardByIdQueryHandler(ICreditCardRepository repository, IApplicationDbContext context)
    : IRequestHandler<GetCreditCardByIdQuery, CreditCardDto>
{
    public async Task<CreditCardDto> Handle(GetCreditCardByIdQuery request, CancellationToken cancellationToken)
    {
        var card = await repository.GetWithBankAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException(nameof(CreditCard), request.Id);

        var transactionCount = await context.Transactions.CountAsync(t => t.CreditCardId == request.Id, cancellationToken);
        var statementCount = await context.Statements.CountAsync(s => s.CreditCardId == request.Id, cancellationToken);

        return card.ToDto(card.Bank?.Name ?? "Unknown", transactionCount, statementCount);
    }
}

public sealed class GetCreditCardLookupQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCreditCardLookupQuery, IReadOnlyList<LookupDto>>
{
    public async Task<IReadOnlyList<LookupDto>> Handle(GetCreditCardLookupQuery request, CancellationToken cancellationToken) =>
        await context.CreditCards
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Nickname)
            .Select(c => new LookupDto(
                c.Id,
                c.Nickname ?? c.Bank!.ShortName + " ****" + c.CardNumber.Last4,
                c.Bank!.Name))
            .ToListAsync(cancellationToken);
}

public sealed class CreateCreditCardCommandHandler(
    ICreditCardRepository repository,
    IBankRepository bankRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateCreditCardCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateCreditCardCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        _ = await bankRepository.GetByIdAsync(request.BankId, cancellationToken)
            ?? throw new NotFoundException(nameof(Bank), request.BankId);

        var last4 = request.CardNumber.Last4();
        if (await repository.FindByLast4Async(request.BankId, last4, cancellationToken) is not null)
        {
            throw new ConflictException("card.duplicate", "A card ending with these digits already exists for this bank.");
        }

        var card = CreditCard.Create(
            request.BankId,
            request.CardNumber,
            request.CardHolderName,
            (CardNetwork)request.Network,
            request.Nickname,
            request.ProductName,
            request.CreditLimit);

        card.Update(
            request.CardHolderName,
            (CardNetwork)request.Network,
            request.Nickname,
            request.ProductName,
            request.CreditLimit,
            request.CashLimit,
            request.StatementDayOfMonth,
            request.PaymentDueDayOfMonth,
            request.ExpiryDate,
            request.Notes);

        card.SetActive(request.IsActive);

        await repository.AddAsync(card, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(card.Id);
    }
}

public sealed class UpdateCreditCardCommandHandler(ICreditCardRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCreditCardCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCreditCardCommand command, CancellationToken cancellationToken)
    {
        var card = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                   ?? throw new NotFoundException(nameof(CreditCard), command.Id);

        var request = command.Request;
        card.Update(
            request.CardHolderName,
            (CardNetwork)request.Network,
            request.Nickname,
            request.ProductName,
            request.CreditLimit,
            request.CashLimit,
            request.StatementDayOfMonth,
            request.PaymentDueDayOfMonth,
            request.ExpiryDate,
            request.Notes);

        card.SetActive(request.IsActive);

        repository.Update(card);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteCreditCardCommandHandler(
    ICreditCardRepository repository,
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeleteCreditCardCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCreditCardCommand command, CancellationToken cancellationToken)
    {
        var card = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                   ?? throw new NotFoundException(nameof(CreditCard), command.Id);

        if (await context.Transactions.AnyAsync(t => t.CreditCardId == command.Id, cancellationToken))
        {
            throw new ConflictException("card.in_use", "This card has transactions and cannot be deleted.");
        }

        repository.SoftDelete(card, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class SaveCreditCardRequestValidator : AbstractValidator<SaveCreditCardRequest>
{
    public SaveCreditCardRequestValidator()
    {
        RuleFor(x => x.BankId).NotEmpty();
        RuleFor(x => x.CardNumber)
            .NotEmpty()
            .Must(n => n.DigitsOnly().Length >= 4)
            .WithMessage("The card number must expose at least the last 4 digits.");
        RuleFor(x => x.CardHolderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Network).Must(v => Enum.IsDefined(typeof(CardNetwork), v)).WithMessage("Unknown card network.");
        RuleFor(x => x.Nickname).MaximumLength(100);
        RuleFor(x => x.ProductName).MaximumLength(150);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0).When(x => x.CreditLimit.HasValue);
        RuleFor(x => x.CashLimit).GreaterThanOrEqualTo(0).When(x => x.CashLimit.HasValue);
        RuleFor(x => x.StatementDayOfMonth).InclusiveBetween(1, 31).When(x => x.StatementDayOfMonth.HasValue);
        RuleFor(x => x.PaymentDueDayOfMonth).InclusiveBetween(1, 31).When(x => x.PaymentDueDayOfMonth.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateCreditCardCommandValidator : AbstractValidator<CreateCreditCardCommand>
{
    public CreateCreditCardCommandValidator() => RuleFor(x => x.Request).NotNull().SetValidator(new SaveCreditCardRequestValidator());
}

public sealed class UpdateCreditCardCommandValidator : AbstractValidator<UpdateCreditCardCommand>
{
    public UpdateCreditCardCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new SaveCreditCardRequestValidator());
    }
}

public sealed class DeleteCreditCardCommandValidator : AbstractValidator<DeleteCreditCardCommand>
{
    public DeleteCreditCardCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
