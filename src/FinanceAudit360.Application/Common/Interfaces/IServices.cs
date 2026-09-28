using FinanceAudit360.Contracts.Auth;
using FinanceAudit360.Domain.Entities;

namespace FinanceAudit360.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? UserName { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    IReadOnlyList<string> Roles { get; }

    IReadOnlyList<string> Permissions { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    string? CorrelationId { get; }

    bool IsInRole(string role);

    bool HasPermission(string permission);
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    DateTime Now { get; }

    DateOnly Today { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);

    /// <summary>True when the stored hash uses outdated parameters and should be upgraded on next login.</summary>
    bool NeedsRehash(string hash);
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresOnUtc) CreateAccessToken(User user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions);

    (string Token, string TokenHash, DateTime ExpiresOnUtc) CreateRefreshToken();

    string HashRefreshToken(string token);
}

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    Task RevokeAsync(RevokeTokenRequest request, CancellationToken cancellationToken = default);

    Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, string subFolder, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    Task WriteTextAsync(string relativePath, string content, CancellationToken cancellationToken = default);

    string GetAbsolutePath(string relativePath);
}

public sealed record StoredFile(string StoredFileName, string RelativePath, long SizeInBytes, string ContentHash);

public interface IAuditLogger
{
    Task LogAsync(AuditLog log, CancellationToken cancellationToken = default);

    Task LogSecurityAsync(Domain.Enums.AuditAction action, string message, CancellationToken cancellationToken = default);
}
