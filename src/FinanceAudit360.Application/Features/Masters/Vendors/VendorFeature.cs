using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Masters.Vendors;

public sealed record SearchVendorsQuery(MasterFilterRequest Filter) : IRequest<PagedResult<VendorDto>>;

public sealed record GetVendorLookupQuery : IRequest<IReadOnlyList<LookupDto>>;

public sealed record CreateVendorCommand(SaveVendorRequest Request) : IRequest<IdResponse>;

public sealed record UpdateVendorCommand(Guid Id, SaveVendorRequest Request) : IRequest<Unit>;

public sealed record DeleteVendorCommand(Guid Id) : IRequest<Unit>;

public sealed class SearchVendorsQueryHandler(IVendorRepository repository)
    : IRequestHandler<SearchVendorsQuery, PagedResult<VendorDto>>
{
    public async Task<PagedResult<VendorDto>> Handle(SearchVendorsQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var page = await repository.SearchAsync(filter.Keyword, filter.IsActive, filter.PageNumber, filter.PageSize, cancellationToken);
        var items = page.Items.Select(p => p.Vendor.ToDto(p.DefaultCategoryName, p.TransactionCount, p.TotalSpend)).ToList();

        return new PagedResult<VendorDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

public sealed class GetVendorLookupQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetVendorLookupQuery, IReadOnlyList<LookupDto>>
{
    public async Task<IReadOnlyList<LookupDto>> Handle(GetVendorLookupQuery request, CancellationToken cancellationToken) =>
        await context.Vendors
            .AsNoTracking()
            .Where(v => v.IsActive)
            .OrderBy(v => v.Name)
            .Select(v => new LookupDto(v.Id, v.DisplayName ?? v.Name, v.Category))
            .ToListAsync(cancellationToken);
}

public sealed class CreateVendorCommandHandler(IVendorRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateVendorCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateVendorCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var normalized = request.Name.Trim().ToUpperInvariant();

        if (await repository.AnyAsync(v => v.NormalizedName == normalized, cancellationToken))
        {
            throw new ConflictException("vendor.duplicate", $"A vendor named '{request.Name}' already exists.");
        }

        var vendor = Vendor.Create(request.Name, request.MatchKeywords, request.DefaultCategoryId);
        vendor.Update(request.Name, request.DisplayName, request.Category, request.DefaultCategoryId, request.Website, request.MatchKeywords, request.Notes);
        vendor.SetActive(request.IsActive);

        await repository.AddAsync(vendor, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(vendor.Id);
    }
}

public sealed class UpdateVendorCommandHandler(IVendorRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateVendorCommand, Unit>
{
    public async Task<Unit> Handle(UpdateVendorCommand command, CancellationToken cancellationToken)
    {
        var vendor = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                     ?? throw new NotFoundException(nameof(Vendor), command.Id);

        var request = command.Request;
        vendor.Update(request.Name, request.DisplayName, request.Category, request.DefaultCategoryId, request.Website, request.MatchKeywords, request.Notes);
        vendor.SetActive(request.IsActive);

        repository.Update(vendor);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteVendorCommandHandler(
    IVendorRepository repository,
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeleteVendorCommand, Unit>
{
    public async Task<Unit> Handle(DeleteVendorCommand command, CancellationToken cancellationToken)
    {
        var vendor = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                     ?? throw new NotFoundException(nameof(Vendor), command.Id);

        if (await context.Transactions.AnyAsync(t => t.VendorId == command.Id, cancellationToken))
        {
            throw new ConflictException("vendor.in_use", "This vendor is assigned to transactions.");
        }

        repository.SoftDelete(vendor, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class SaveVendorRequestValidator : AbstractValidator<SaveVendorRequest>
{
    public SaveVendorRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.DisplayName).MaximumLength(250);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.Website).MaximumLength(300);
        RuleFor(x => x.MatchKeywords).MaximumLength(2000);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateVendorCommandValidator : AbstractValidator<CreateVendorCommand>
{
    public CreateVendorCommandValidator() => RuleFor(x => x.Request).NotNull().SetValidator(new SaveVendorRequestValidator());
}

public sealed class UpdateVendorCommandValidator : AbstractValidator<UpdateVendorCommand>
{
    public UpdateVendorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new SaveVendorRequestValidator());
    }
}

public sealed class DeleteVendorCommandValidator : AbstractValidator<DeleteVendorCommand>
{
    public DeleteVendorCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
