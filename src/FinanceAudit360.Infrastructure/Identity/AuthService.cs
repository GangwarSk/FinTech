using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Contracts.Auth;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceAudit360.Infrastructure.Identity;

public sealed class AuthService(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenService tokenService,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    IAuditLogger auditLogger,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identifier = request.UserNameOrEmail.Trim().ToUpperInvariant();

        var user = await context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r!.Permissions)
            .Include(u => u.RefreshTokens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                u => u.NormalizedUserName == identifier || u.NormalizedEmail == identifier,
                cancellationToken);

        // The same message is returned for unknown users and bad passwords to avoid account enumeration.
        if (user is null)
        {
            await auditLogger.LogSecurityAsync(AuditAction.LoginFailed, $"Unknown identifier '{request.UserNameOrEmail}'.", cancellationToken);
            throw new UnauthorizedException("Invalid credentials.");
        }

        if (user.IsLockedOut(dateTime.UtcNow))
        {
            await auditLogger.LogSecurityAsync(AuditAction.LoginFailed, $"Locked out user '{user.UserName}'.", cancellationToken);
            throw new UnauthorizedException("The account is temporarily locked. Try again later.");
        }

        if (!user.IsActive)
        {
            await auditLogger.LogSecurityAsync(AuditAction.LoginFailed, $"Inactive user '{user.UserName}'.", cancellationToken);
            throw new UnauthorizedException("The account is disabled.");
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(dateTime.UtcNow);
            await context.SaveChangesAsync(cancellationToken);
            await auditLogger.LogSecurityAsync(AuditAction.LoginFailed, $"Bad password for '{user.UserName}'.", cancellationToken);
            throw new UnauthorizedException("Invalid credentials.");
        }

        if (passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.SetPassword(passwordHasher.Hash(request.Password), user.MustChangePassword);
            logger.LogInformation("Upgraded password hash for {UserName}.", user.UserName);
        }

        user.RegisterSuccessfulLogin(dateTime.UtcNow);

        var response = await IssueTokensAsync(user, cancellationToken);
        await auditLogger.LogSecurityAsync(AuditAction.Login, $"User '{user.UserName}' signed in.", cancellationToken);

        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);

        var token = await context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new UnauthorizedException("The refresh token is not recognised.");

        if (!token.IsActive(dateTime.UtcNow))
        {
            // A reused or revoked token means the chain may be compromised, so kill every session.
            var compromised = await context.Users
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);

            compromised?.RevokeAllRefreshTokens("refresh-token-reuse-detected");
            await context.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedException("The refresh token is no longer valid.");
        }

        var user = await context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r!.Permissions)
            .Include(u => u.RefreshTokens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken)
            ?? throw new UnauthorizedException("The account no longer exists.");

        if (!user.IsActive)
        {
            throw new UnauthorizedException("The account is disabled.");
        }

        var response = await IssueTokensAsync(user, cancellationToken, rotatedFrom: token);
        return response;
    }

    public async Task RevokeAsync(RevokeTokenRequest request, CancellationToken cancellationToken = default)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var token = await context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null)
        {
            return;
        }

        token.Revoke("logout", null);
        await context.SaveChangesAsync(cancellationToken);
        await auditLogger.LogSecurityAsync(AuditAction.Logout, "Refresh token revoked.", cancellationToken);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r!.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        var (roles, permissions) = Resolve(user);

        return new CurrentUserDto(
            user.Id,
            user.UserName,
            user.Email,
            user.FullName,
            user.MustChangePassword,
            user.LastLoginOnUtc,
            roles,
            permissions);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new ValidationException(nameof(request.CurrentPassword), "The current password is incorrect.");
        }

        user.SetPassword(passwordHasher.Hash(request.NewPassword));
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(
        User user,
        CancellationToken cancellationToken,
        RefreshToken? rotatedFrom = null)
    {
        var (roles, permissions) = Resolve(user);

        var (accessToken, accessExpiry) = tokenService.CreateAccessToken(user, roles, permissions);
        var (refreshToken, refreshHash, refreshExpiry) = tokenService.CreateRefreshToken();

        user.IssueRefreshToken(refreshHash, refreshExpiry, currentUser.IpAddress, currentUser.UserAgent);
        rotatedFrom?.Revoke("rotated", refreshHash);

        await context.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            refreshToken,
            accessExpiry,
            refreshExpiry,
            new CurrentUserDto(
                user.Id,
                user.UserName,
                user.Email,
                user.FullName,
                user.MustChangePassword,
                user.LastLoginOnUtc,
                roles,
                permissions));
    }

    private static (IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions) Resolve(User user)
    {
        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .Distinct()
            .ToList();

        var permissions = user.UserRoles
            .Where(ur => ur.Role is not null)
            .SelectMany(ur => ur.Role!.Permissions.Select(p => p.Permission))
            .Distinct()
            .ToList();

        return (roles, permissions);
    }
}
