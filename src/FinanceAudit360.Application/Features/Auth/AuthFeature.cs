using FinanceAudit360.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using FinanceAudit360.Contracts.Auth;

namespace FinanceAudit360.Application.Features.Auth;

public sealed record LoginCommand(string UserNameOrEmail, string Password) : IRequest<AuthResponse>;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

public sealed record RevokeTokenCommand(string RefreshToken) : IRequest<Unit>;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Unit>;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<CurrentUserDto>;

public sealed class LoginCommandHandler(IAuthService authService) : IRequestHandler<LoginCommand, AuthResponse>
{
    public Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        authService.LoginAsync(new LoginRequest(request.UserNameOrEmail, request.Password), cancellationToken);
}

public sealed class RefreshTokenCommandHandler(IAuthService authService) : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken) =>
        authService.RefreshAsync(new RefreshTokenRequest(request.RefreshToken), cancellationToken);
}

public sealed class RevokeTokenCommandHandler(IAuthService authService) : IRequestHandler<RevokeTokenCommand, Unit>
{
    public async Task<Unit> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        await authService.RevokeAsync(new RevokeTokenRequest(request.RefreshToken), cancellationToken);
        return Unit.Value;
    }
}

public sealed class ChangePasswordCommandHandler(IAuthService authService) : IRequestHandler<ChangePasswordCommand, Unit>
{
    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(
            request.UserId,
            new ChangePasswordRequest(request.CurrentPassword, request.NewPassword),
            cancellationToken);

        return Unit.Value;
    }
}

public sealed class GetCurrentUserQueryHandler(IAuthService authService) : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public Task<CurrentUserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken) =>
        authService.GetCurrentUserAsync(request.UserId, cancellationToken);
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserNameOrEmail).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an upper case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain a special character.")
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must differ from the current password.");
    }
}
