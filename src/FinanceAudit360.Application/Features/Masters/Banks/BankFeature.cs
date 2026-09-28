using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Extensions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Masters.Banks;

public sealed record SearchBanksQuery(MasterFilterRequest Filter) : IRequest<PagedResult<BankDto>>;

public sealed record GetBankByIdQuery(Guid Id) : IRequest<BankDto>;

public sealed record GetBankLookupQuery : IRequest<IReadOnlyList<LookupDto>>;

public sealed record CreateBankCommand(SaveBankRequest Request) : IRequest<IdResponse>;

public sealed record UpdateBankCommand(Guid Id, SaveBankRequest Request) : IRequest<Unit>;

public sealed record DeleteBankCommand(Guid Id) : IRequest<Unit>;

public sealed class SearchBanksQueryHandler(IApplicationDbContext context) : IRequestHandler<SearchBanksQuery, PagedResult<BankDto>>
{
    public async Task<PagedResult<BankDto>> Handle(SearchBanksQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = context.Banks
            .AsNoTracking()
            .WhereIf(filter.IsActive.HasValue, b => b.IsActive == filter.IsActive!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword),
                b => b.Name.ToUpper().Contains(keyword!) || b.ShortName.ToUpper().Contains(keyword!))
            .ApplySort(filter.SortBy, filter.SortDirection, nameof(Bank.Name))
            .Select(ProjectionMappings.BankProjection);

        return await query.ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);
    }
}

public sealed class GetBankByIdQueryHandler(IApplicationDbContext context) : IRequestHandler<GetBankByIdQuery, BankDto>
{
    public async Task<BankDto> Handle(GetBankByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await context.Banks
            .AsNoTracking()
            .Where(b => b.Id == request.Id)
            .Select(ProjectionMappings.BankProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException(nameof(Bank), request.Id);
    }
}

public sealed class GetBankLookupQueryHandler(IApplicationDbContext context) : IRequestHandler<GetBankLookupQuery, IReadOnlyList<LookupDto>>
{
    public async Task<IReadOnlyList<LookupDto>> Handle(GetBankLookupQuery request, CancellationToken cancellationToken) =>
        await context.Banks
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new LookupDto(b.Id, b.Name, b.ShortName))
            .ToListAsync(cancellationToken);
}

public sealed class CreateBankCommandHandler(IBankRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateBankCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateBankCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var name = request.Name.Trim();

        if (await repository.AnyAsync(b => b.Name == name, cancellationToken))
        {
            throw new ConflictException("bank.duplicate", $"A bank named '{name}' already exists.");
        }

        var bank = Bank.Create(request.Name, request.ShortName, (BankCode)request.Code, request.StatementKeywords);
        bank.Update(request.Name, request.ShortName, (BankCode)request.Code, request.Ifsc, request.Website, request.LogoUrl, request.StatementKeywords);
        bank.SetActive(request.IsActive);

        await repository.AddAsync(bank, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(bank.Id);
    }
}

public sealed class UpdateBankCommandHandler(IBankRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateBankCommand, Unit>
{
    public async Task<Unit> Handle(UpdateBankCommand command, CancellationToken cancellationToken)
    {
        var bank = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                   ?? throw new NotFoundException(nameof(Bank), command.Id);

        var request = command.Request;
        bank.Update(request.Name, request.ShortName, (BankCode)request.Code, request.Ifsc, request.Website, request.LogoUrl, request.StatementKeywords);
        bank.SetActive(request.IsActive);

        repository.Update(bank);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteBankCommandHandler(
    IBankRepository repository,
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeleteBankCommand, Unit>
{
    public async Task<Unit> Handle(DeleteBankCommand command, CancellationToken cancellationToken)
    {
        var bank = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                   ?? throw new NotFoundException(nameof(Bank), command.Id);

        var inUse = await context.BankAccounts.AnyAsync(a => a.BankId == command.Id, cancellationToken)
                    || await context.CreditCards.AnyAsync(c => c.BankId == command.Id, cancellationToken);

        if (inUse)
        {
            throw new ConflictException("bank.in_use", "This bank still has accounts or cards linked to it.");
        }

        repository.SoftDelete(bank, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class SaveBankRequestValidator : AbstractValidator<SaveBankRequest>
{
    public SaveBankRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Code).Must(c => Enum.IsDefined(typeof(BankCode), c)).WithMessage("Unknown bank code.");
        RuleFor(x => x.Ifsc).MaximumLength(20);
        RuleFor(x => x.Website).MaximumLength(300);
        RuleFor(x => x.StatementKeywords).MaximumLength(1000);
    }
}

public sealed class CreateBankCommandValidator : AbstractValidator<CreateBankCommand>
{
    public CreateBankCommandValidator() => RuleFor(x => x.Request).NotNull().SetValidator(new SaveBankRequestValidator());
}

public sealed class UpdateBankCommandValidator : AbstractValidator<UpdateBankCommand>
{
    public UpdateBankCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new SaveBankRequestValidator());
    }
}

public sealed class DeleteBankCommandValidator : AbstractValidator<DeleteBankCommand>
{
    public DeleteBankCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
