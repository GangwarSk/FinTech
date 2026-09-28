using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Extensions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Masters.Users;

public sealed record SearchUsersQuery(MasterFilterRequest Filter) : IRequest<PagedResult<UserDto>>;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<UserDto>;

public sealed record CreateUserCommand(CreateUserRequest Request) : IRequest<IdResponse>;

public sealed record UpdateUserCommand(Guid Id, UpdateUserRequest Request) : IRequest<Unit>;

public sealed record DeleteUserCommand(Guid Id) : IRequest<Unit>;

public sealed record ResetUserPasswordCommand(Guid Id, ResetPasswordRequest Request) : IRequest<Unit>;

public sealed record SearchRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public sealed record CreateRoleCommand(SaveRoleRequest Request) : IRequest<IdResponse>;

public sealed record UpdateRoleCommand(Guid Id, SaveRoleRequest Request) : IRequest<Unit>;

public sealed record DeleteRoleCommand(Guid Id) : IRequest<Unit>;

public sealed record GetPermissionCatalogQuery : IRequest<IReadOnlyList<string>>;

public sealed class SearchUsersQueryHandler(IApplicationDbContext context) : IRequestHandler<SearchUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var keyword = filter.Keyword?.Trim().ToUpperInvariant();

        var query = context.Users
            .AsNoTracking()
            .WhereIf(filter.IsActive.HasValue, u => u.IsActive == filter.IsActive!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(keyword),
                u => u.NormalizedUserName.Contains(keyword!) ||
                     u.NormalizedEmail.Contains(keyword!) ||
                     u.FullName.ToUpper().Contains(keyword!))
            .ApplySort(filter.SortBy, filter.SortDirection, nameof(User.UserName))
            .Select(u => new UserDto(
                u.Id,
                u.UserName,
                u.Email,
                u.FullName,
                u.PhoneNumber,
                u.IsActive,
                u.MustChangePassword,
                u.LastLoginOnUtc,
                u.CreatedOnUtc,
                u.UserRoles.Select(r => new LookupRoleDto(r.RoleId, r.Role!.Name)).ToList()));

        return await query.ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);
    }
}

public sealed class GetUserByIdQueryHandler(IApplicationDbContext context) : IRequestHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.Id)
            .Select(u => new UserDto(
                u.Id,
                u.UserName,
                u.Email,
                u.FullName,
                u.PhoneNumber,
                u.IsActive,
                u.MustChangePassword,
                u.LastLoginOnUtc,
                u.CreatedOnUtc,
                u.UserRoles.Select(r => new LookupRoleDto(r.RoleId, r.Role!.Name)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException(nameof(User), request.Id);
    }
}

public sealed class CreateUserCommandHandler(
    IUnitOfWork unitOfWork,
    IApplicationDbContext context,
    IPasswordHasher passwordHasher) : IRequestHandler<CreateUserCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var exists = await context.Users
            .AnyAsync(u => u.NormalizedUserName == normalizedUserName || u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new ConflictException("user.duplicate", "A user with this name or email already exists.");
        }

        var user = User.Create(request.UserName, request.Email, request.FullName, passwordHasher.Hash(request.Password), request.PhoneNumber);
        user.ReplaceRoles(request.RoleIds);

        await unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(user.Id);
    }
}

public sealed class UpdateUserCommandHandler(IUnitOfWork unitOfWork, IApplicationDbContext context)
    : IRequestHandler<UpdateUserCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.Id);

        var request = command.Request;
        user.UpdateProfile(request.FullName, request.PhoneNumber);
        user.ChangeEmail(request.Email);
        user.ReplaceRoles(request.RoleIds);

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class DeleteUserCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == command.Id)
        {
            throw new ConflictException("user.self_delete", "You cannot delete your own account.");
        }

        var repository = unitOfWork.Repository<User>();
        var user = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                   ?? throw new NotFoundException(nameof(User), command.Id);

        user.Deactivate();
        repository.SoftDelete(user, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class ResetUserPasswordCommandHandler(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher) : IRequestHandler<ResetUserPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<User>();
        var user = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                   ?? throw new NotFoundException(nameof(User), command.Id);

        user.SetPassword(passwordHasher.Hash(command.Request.NewPassword), command.Request.MustChangePassword);

        repository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class SearchRolesQueryHandler(IApplicationDbContext context) : IRequestHandler<SearchRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(SearchRolesQuery request, CancellationToken cancellationToken) =>
        await context.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystemRole,
                r.UserRoles.Count,
                r.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList()))
            .ToListAsync(cancellationToken);
}

public sealed class CreateRoleCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateRoleCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Role>();
        var normalized = command.Request.Name.Trim().ToUpperInvariant();

        if (await repository.AnyAsync(r => r.NormalizedName == normalized, cancellationToken))
        {
            throw new ConflictException("role.duplicate", "A role with this name already exists.");
        }

        var role = Role.Create(command.Request.Name, command.Request.Description);
        role.ReplacePermissions(command.Request.Permissions);

        await repository.AddAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(role.Id);
    }
}

public sealed class UpdateRoleCommandHandler(IUnitOfWork unitOfWork, IApplicationDbContext context)
    : IRequestHandler<UpdateRoleCommand, Unit>
{
    public async Task<Unit> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), command.Id);

        if (!role.IsSystemRole)
        {
            role.Rename(command.Request.Name);
        }

        role.UpdateDescription(command.Request.Description);
        role.ReplacePermissions(command.Request.Permissions);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class DeleteRoleCommandHandler(
    IUnitOfWork unitOfWork,
    IApplicationDbContext context,
    ICurrentUser currentUser) : IRequestHandler<DeleteRoleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Role>();
        var role = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                   ?? throw new NotFoundException(nameof(Role), command.Id);

        if (role.IsSystemRole)
        {
            throw new ConflictException("role.system", "System roles cannot be deleted.");
        }

        if (await context.UserRoles.AnyAsync(ur => ur.RoleId == command.Id, cancellationToken))
        {
            throw new ConflictException("role.in_use", "This role is assigned to users.");
        }

        repository.SoftDelete(role, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Permissions.All);
}

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Request.UserName).NotEmpty().MaximumLength(100)
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("User name may only contain letters, digits, dot, underscore and hyphen.");
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Request.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Password)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an upper case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain a special character.");
        RuleFor(x => x.Request.RoleIds).NotEmpty().WithMessage("Assign at least one role.");
    }
}

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Request.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.RoleIds).NotEmpty();
    }
}

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.NewPassword)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(128)
            .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]").Matches(@"[^a-zA-Z0-9]")
            .WithMessage("Password must include upper case, lower case, digit and special characters.");
    }
}

public sealed class SaveRoleRequestValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleForEach(x => x.Permissions)
            .Must(p => Permissions.All.Contains(p))
            .WithMessage("Unknown permission '{PropertyValue}'.");
    }
}

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator() => RuleFor(x => x.Request).NotNull().SetValidator(new SaveRoleRequestValidator());
}

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new SaveRoleRequestValidator());
    }
}
