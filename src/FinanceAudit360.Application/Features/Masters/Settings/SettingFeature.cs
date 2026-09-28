using AutoMapper;
using AutoMapper.QueryableExtensions;
using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceAudit360.Application.Features.Masters.Settings;

public sealed record GetSettingsQuery(string? Category) : IRequest<IReadOnlyList<SettingDto>>;

public sealed record CreateSettingCommand(SaveSettingRequest Request) : IRequest<IdResponse>;

public sealed record UpdateSettingValueCommand(string Key, UpdateSettingValueRequest Request) : IRequest<Unit>;

public sealed record DeleteSettingCommand(Guid Id) : IRequest<Unit>;

public sealed class GetSettingsQueryHandler(IApplicationDbContext context, IMapper mapper)
    : IRequestHandler<GetSettingsQuery, IReadOnlyList<SettingDto>>
{
    public async Task<IReadOnlyList<SettingDto>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Settings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim();
            query = query.Where(s => s.Category == category);
        }

        var settings = await query
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ProjectTo<SettingDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        // Secret values are never returned; only the fact that a value exists.
        return settings
            .Select(s => s.IsSecret ? s with { Value = string.IsNullOrEmpty(s.Value) ? null : "********" } : s)
            .ToList();
    }
}

public sealed class CreateSettingCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateSettingCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateSettingCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Setting>();
        var key = command.Request.Key.Trim();

        if (await repository.AnyAsync(s => s.Key == key, cancellationToken))
        {
            throw new ConflictException("setting.duplicate", $"A setting with key '{key}' already exists.");
        }

        var setting = Setting.Create(
            command.Request.Key,
            command.Request.Value,
            (SettingDataType)command.Request.DataType,
            command.Request.Category,
            command.Request.Description);

        await repository.AddAsync(setting, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdResponse(setting.Id);
    }
}

public sealed class UpdateSettingValueCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateSettingValueCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSettingValueCommand command, CancellationToken cancellationToken)
    {
        var setting = await context.Settings.FirstOrDefaultAsync(s => s.Key == command.Key, cancellationToken)
                      ?? throw new NotFoundException(nameof(Setting), command.Key);

        setting.SetValue(command.Request.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteSettingCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    : IRequestHandler<DeleteSettingCommand, Unit>
{
    public async Task<Unit> Handle(DeleteSettingCommand command, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Setting>();
        var setting = await repository.GetByIdAsync(command.Id, asNoTracking: false, cancellationToken)
                      ?? throw new NotFoundException(nameof(Setting), command.Id);

        if (setting.IsSystem)
        {
            throw new ConflictException("setting.system", "System settings cannot be deleted.");
        }

        repository.SoftDelete(setting, currentUser.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class CreateSettingCommandValidator : AbstractValidator<CreateSettingCommand>
{
    public CreateSettingCommandValidator()
    {
        RuleFor(x => x.Request.Key).NotEmpty().MaximumLength(150)
            .Matches("^[A-Za-z0-9._:-]+$").WithMessage("The key may only contain letters, digits, dot, colon, underscore and hyphen.");
        RuleFor(x => x.Request.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Value).MaximumLength(4000);
        RuleFor(x => x.Request.Description).MaximumLength(500);
        RuleFor(x => x.Request.DataType).Must(v => Enum.IsDefined(typeof(SettingDataType), v)).WithMessage("Unknown data type.");
    }
}

public sealed class UpdateSettingValueCommandValidator : AbstractValidator<UpdateSettingValueCommand>
{
    public UpdateSettingValueCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Request.Value).MaximumLength(4000);
    }
}
