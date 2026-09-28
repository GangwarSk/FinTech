namespace FinanceAudit360.Contracts.Auth;

public sealed record LoginRequest(string UserNameOrEmail, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record RevokeTokenRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresOnUtc,
    DateTime RefreshTokenExpiresOnUtc,
    CurrentUserDto User);

public sealed record CurrentUserDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    bool MustChangePassword,
    DateTime? LastLoginOnUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
