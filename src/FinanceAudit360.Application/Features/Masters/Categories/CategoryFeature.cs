using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Extensions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Masters.Categories;

public sealed record SearchCategoriesQuery(MasterFilterRequest Filter) : IRequest<PagedResult<CategoryDto>>;

public sealed record GetCategoryLookupQuery : IRequest<IReadOnlyList<LookupDto>>;

public sealed record CreateCategoryCommand(SaveCategoryRequest Request) : IRequest<IdResponse>;

public sealed record UpdateCategoryCommand(Guid Id, SaveCategoryRequest Request) : IRequest<Unit>;

public sealed record DeleteCategoryCommand(Guid Id) : IRequest<Unit>;

public sealed class SearchCategoriesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchCategoriesQuery, PagedResult<CategoryDto>>
{
    public async Task<PagedResult<CategoryDto>> Handle(SearchCategoriesQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = context.Categories
            .AsNoTracking()
            .WhereIf(filter.IsActive.HasValue, c => c.IsActive == filter.IsActive!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword), c => c.Name.ToUpper().Contains(keyword!))
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Code,
                c.Description,
                c.ColorHex,
                c.Icon,
                c.ParentCategoryId,
                c.ParentCategory != null ? c.ParentCategory.Name : null,
                c.IsSystemCategory,
                c.IsActive,
                c.DisplayOrder,
                c.MatchKeywords,
                context.Transactions.Count(t => t.CategoryId == c.Id)));

        return await query.ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);
    }
}

public sealed class GetCategoryLookupQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCategoryLookupQuery, IReadOnlyList<LookupDto>>
{
    public async Task<IReadOnlyList<LookupDto>> Handle(GetCategoryLookupQuery request, CancellationToken cancellationToken) =>
        await context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new LookupDto(c.Id, c.Name, c.Code))
            .ToListAsync(cancellationToken);
}

public sealed class CreateCategoryCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateCategoryCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<TransactionCategory>();
        var request = command.Request;
        var name = request.Name.Trim();

        if (await repository.AnyAsync(c => c.Name == name, cancellationToken))
        {
            throw new ConflictException("category.duplicate", $"A category named '{name}' already exists.");
        }

        var category = TransactionCategory.Create(request.Name, request.Code, request.ParentCategoryId, request.MatchKeywords);
        category.Update(request.Name, request.Code, request.Description, request.ColorHex, request.Icon, request.ParentCategoryId, request.MatchKeywords, request.DisplayOrder);
        category.SetActive(request.IsActive);

        await repository.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(category.Id);
    }
}

public sealed class UpdateCategoryCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<UpdateCategoryCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<TransactionCategory>();
        var category = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                       ?? throw new NotFoundException(nameof(TransactionCategory), command.Id);

        var request = command.Request;
        category.Update(request.Name, request.Code, request.Description, request.ColorHex, request.Icon, request.ParentCategoryId, request.MatchKeywords, request.DisplayOrder);
        category.SetActive(request.IsActive);

        repository.Update(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteCategoryCommandHandler(
    IUnitOfWork unitOfWork,
    IApplicationDbContext context,
    ICurrentUser currentUser) : IRequestHandler<DeleteCategoryCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<TransactionCategory>();
        var category = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                       ?? throw new NotFoundException(nameof(TransactionCategory), command.Id);

        if (category.IsSystemCategory)
        {
            throw new ConflictException("category.system", "System categories cannot be deleted.");
        }

        if (await context.Transactions.AnyAsync(t => t.CategoryId == command.Id, cancellationToken))
        {
            throw new ConflictException("category.in_use", "This category is assigned to transactions.");
        }

        repository.SoftDelete(category, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.ColorHex).Matches("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.ColorHex))
            .WithMessage("Colour must be a hex value such as #2563eb.");
        RuleFor(x => x.MatchKeywords).MaximumLength(1000);
        RuleFor(x => x.DisplayOrder).InclusiveBetween(0, 9999);
    }
}

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator() => RuleFor(x => x.Request).NotNull().SetValidator(new SaveCategoryRequestValidator());
}

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new SaveCategoryRequestValidator());
    }
}

public sealed class DeleteCategoryCommandValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
