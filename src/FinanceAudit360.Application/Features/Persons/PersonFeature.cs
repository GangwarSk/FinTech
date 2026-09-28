using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Persons;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Persons;

public sealed record SearchPersonsQuery(PersonFilterRequest Filter) : IRequest<PagedResult<PersonDto>>;

public sealed record GetPersonByIdQuery(Guid Id) : IRequest<PersonDto>;

public sealed record GetPersonAuditQuery(Guid Id, DateTime? From, DateTime? To) : IRequest<PersonAuditDto>;

public sealed record GetPersonLedgerQuery(Guid PersonId, PersonLedgerFilterRequest Filter) : IRequest<PagedResult<LedgerEntryDto>>;

public sealed record CreatePersonCommand(CreatePersonRequest Request) : IRequest<IdResponse>;

public sealed record UpdatePersonCommand(Guid Id, UpdatePersonRequest Request) : IRequest<Unit>;

public sealed record DeletePersonCommand(Guid Id) : IRequest<Unit>;

public sealed record AddLedgerEntryCommand(Guid PersonId, CreateLedgerEntryRequest Request) : IRequest<IdResponse>;

public sealed record DeleteLedgerEntryCommand(Guid PersonId, Guid LedgerEntryId) : IRequest<Unit>;

public sealed record SettleLedgerEntryCommand(Guid PersonId, Guid LedgerEntryId, DateTime SettledOn) : IRequest<Unit>;

public sealed class SearchPersonsQueryHandler(IPersonRepository repository)
    : IRequestHandler<SearchPersonsQuery, PagedResult<PersonDto>>
{
    public Task<PagedResult<PersonDto>> Handle(SearchPersonsQuery request, CancellationToken cancellationToken) =>
        repository.SearchAsync(request.Filter, cancellationToken);
}

public sealed class GetPersonByIdQueryHandler(IApplicationDbContext context) : IRequestHandler<GetPersonByIdQuery, PersonDto>
{
    public async Task<PersonDto> Handle(GetPersonByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await context.Persons
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(ProjectionMappings.PersonProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException(nameof(Person), request.Id);
    }
}

public sealed class GetPersonAuditQueryHandler(IPersonRepository repository) : IRequestHandler<GetPersonAuditQuery, PersonAuditDto>
{
    public async Task<PersonAuditDto> Handle(GetPersonAuditQuery request, CancellationToken cancellationToken)
    {
        var audit = await repository.GetAuditAsync(request.Id, request.From, request.To, cancellationToken);
        return audit ?? throw new NotFoundException(nameof(Person), request.Id);
    }
}

public sealed class GetPersonLedgerQueryHandler(IPersonRepository repository)
    : IRequestHandler<GetPersonLedgerQuery, PagedResult<LedgerEntryDto>>
{
    public Task<PagedResult<LedgerEntryDto>> Handle(GetPersonLedgerQuery request, CancellationToken cancellationToken) =>
        repository.GetLedgerAsync(request.PersonId, request.Filter, cancellationToken);
}

public sealed class CreatePersonCommandHandler(
    IPersonRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreatePersonCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreatePersonCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var normalized = request.Name.Trim().ToUpperInvariant();

        if (await repository.AnyAsync(p => p.NormalizedName == normalized, cancellationToken))
        {
            throw new ConflictException("person.duplicate_name", $"A person named '{request.Name}' already exists.");
        }

        var person = Person.Create(request.Name, request.Mobile, request.Email, request.Relationship);
        person.Update(
            request.Name,
            request.Mobile,
            request.Email,
            request.Address.ToValueObject(),
            request.Relationship,
            request.Notes,
            request.MatchKeywords);

        await repository.AddAsync(person, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(person.Id);
    }
}

public sealed class UpdatePersonCommandHandler(
    IPersonRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdatePersonCommand, Unit>
{
    public async Task<Unit> Handle(UpdatePersonCommand command, CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                     ?? throw new NotFoundException(nameof(Person), command.Id);

        var request = command.Request;
        person.Update(
            request.Name,
            request.Mobile,
            request.Email,
            request.Address.ToValueObject(),
            request.Relationship,
            request.Notes,
            request.MatchKeywords);

        person.SetActive(request.IsActive);

        repository.Update(person);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeletePersonCommandHandler(
    IPersonRepository repository,
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeletePersonCommand, Unit>
{
    public async Task<Unit> Handle(DeletePersonCommand command, CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                     ?? throw new NotFoundException(nameof(Person), command.Id);

        var hasTransactions = await context.Transactions.AnyAsync(t => t.PersonId == command.Id, cancellationToken);
        if (hasTransactions)
        {
            throw new ConflictException(
                "person.has_transactions",
                "This person is linked to transactions. Unlink them before deleting.");
        }

        repository.SoftDelete(person, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class AddLedgerEntryCommandHandler(
    IPersonRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<AddLedgerEntryCommand, IdResponse>
{
    public async Task<IdResponse> Handle(AddLedgerEntryCommand command, CancellationToken cancellationToken)
    {
        var person = await repository.GetWithLedgerAsync(command.PersonId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Person), command.PersonId);

        var request = command.Request;
        var entry = person.RecordEntry(
            request.EntryDate,
            request.Amount,
            (LedgerEntryType)request.EntryType,
            request.Description,
            request.TransactionId,
            request.BankAccountId,
            request.CreditCardId,
            request.ReferenceNumber);

        repository.Update(person);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(entry.Id);
    }
}

public sealed class DeleteLedgerEntryCommandHandler(
    IPersonRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : IRequestHandler<DeleteLedgerEntryCommand, Unit>
{
    public async Task<Unit> Handle(DeleteLedgerEntryCommand command, CancellationToken cancellationToken)
    {
        var person = await repository.GetWithLedgerAsync(command.PersonId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Person), command.PersonId);

        person.RemoveEntry(command.LedgerEntryId, currentUser.UserName, dateTime.UtcNow);

        repository.Update(person);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class SettleLedgerEntryCommandHandler(
    IPersonRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<SettleLedgerEntryCommand, Unit>
{
    public async Task<Unit> Handle(SettleLedgerEntryCommand command, CancellationToken cancellationToken)
    {
        var person = await repository.GetWithLedgerAsync(command.PersonId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Person), command.PersonId);

        var entry = person.LedgerEntries.FirstOrDefault(e => e.Id == command.LedgerEntryId)
                    ?? throw new NotFoundException(nameof(MoneyLedger), command.LedgerEntryId);

        entry.Settle(command.SettledOn);

        repository.Update(person);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Mobile).MaximumLength(20);
        RuleFor(x => x.Request.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Request.Email));
        RuleFor(x => x.Request.Relationship).MaximumLength(100);
        RuleFor(x => x.Request.Notes).MaximumLength(2000);
        RuleFor(x => x.Request.MatchKeywords).MaximumLength(1000);
    }
}

public sealed class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Mobile).MaximumLength(20);
        RuleFor(x => x.Request.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Request.Email));
        RuleFor(x => x.Request.Notes).MaximumLength(2000);
        RuleFor(x => x.Request.MatchKeywords).MaximumLength(1000);
    }
}

public sealed class AddLedgerEntryCommandValidator : AbstractValidator<AddLedgerEntryCommand>
{
    public AddLedgerEntryCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.Request.Amount).GreaterThan(0m).LessThanOrEqualTo(100_000_000m);
        RuleFor(x => x.Request.EntryDate).NotEmpty();
        RuleFor(x => x.Request.EntryType)
            .Must(value => Enum.IsDefined(typeof(LedgerEntryType), value))
            .WithMessage("Entry type must be Given (1), Taken (2), SettlementReceived (3) or SettlementPaid (4).");
        RuleFor(x => x.Request.Description).MaximumLength(1000);
        RuleFor(x => x.Request.ReferenceNumber).MaximumLength(100);
    }
}

public sealed class DeletePersonCommandValidator : AbstractValidator<DeletePersonCommand>
{
    public DeletePersonCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class SearchPersonsQueryValidator : AbstractValidator<SearchPersonsQuery>
{
    public SearchPersonsQueryValidator()
    {
        RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.Filter.Keyword).MaximumLength(200);
    }
}
