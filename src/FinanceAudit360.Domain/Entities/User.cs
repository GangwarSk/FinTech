using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Events;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Domain.ValueObjects;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

public class User : AuditableEntity, IAggregateRoot
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly List<UserRole> _userRoles = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    private User()
    {
    }

    private User(string userName, string email, string fullName, string passwordHash)
    {
        UserName = userName;
        NormalizedUserName = userName.ToUpperInvariantSafe();
        Email = email;
        NormalizedEmail = email.ToUpperInvariantSafe();
        FullName = fullName;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    public string UserName { get; private set; } = string.Empty;

    public string NormalizedUserName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public bool IsActive { get; private set; }

    public bool MustChangePassword { get; private set; }

    public DateTime? LastLoginOnUtc { get; private set; }

    public int AccessFailedCount { get; private set; }

    public DateTime? LockoutEndUtc { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public bool IsLockedOut(DateTime utcNow) => LockoutEndUtc.HasValue && LockoutEndUtc.Value > utcNow;

    public static User Create(string userName, string email, string fullName, string passwordHash, string? phoneNumber = null)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new DomainException("user.username_required", "User name is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("user.password_required", "Password hash is required.");
        }

        var validEmail = EmailAddress.Create(email);
        var user = new User(userName.NormalizeText(), validEmail.Value, fullName.NormalizeText(), passwordHash)
        {
            PhoneNumber = ValueObjects.PhoneNumber.CreateOrNull(phoneNumber)?.Value
        };

        user.Raise(new UserRegisteredEvent(user.Id, user.UserName, user.Email));
        return user;
    }

    public void UpdateProfile(string fullName, string? phoneNumber)
    {
        FullName = fullName.NormalizeText();
        PhoneNumber = ValueObjects.PhoneNumber.CreateOrNull(phoneNumber)?.Value;
    }

    public void ChangeEmail(string email)
    {
        var validEmail = EmailAddress.Create(email);
        Email = validEmail.Value;
        NormalizedEmail = Email.ToUpperInvariantSafe();
    }

    public void SetPassword(string passwordHash, bool mustChange = false)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("user.password_required", "Password hash is required.");
        }

        PasswordHash = passwordHash;
        MustChangePassword = mustChange;
        RevokeAllRefreshTokens("password-changed");
    }

    public void Activate() => IsActive = true;

    public void Deactivate()
    {
        IsActive = false;
        RevokeAllRefreshTokens("user-deactivated");
    }

    public void AssignRole(Guid roleId)
    {
        if (_userRoles.Any(r => r.RoleId == roleId))
        {
            return;
        }

        _userRoles.Add(UserRole.Create(Id, roleId));
    }

    public void RemoveRole(Guid roleId) => _userRoles.RemoveAll(r => r.RoleId == roleId);

    public void ReplaceRoles(IEnumerable<Guid> roleIds)
    {
        _userRoles.Clear();
        foreach (var roleId in roleIds.Distinct())
        {
            AssignRole(roleId);
        }
    }

    public void RegisterSuccessfulLogin(DateTime utcNow)
    {
        LastLoginOnUtc = utcNow;
        AccessFailedCount = 0;
        LockoutEndUtc = null;
        Raise(new UserLoggedInEvent(Id, UserName, utcNow));
    }

    public void RegisterFailedLogin(DateTime utcNow)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= MaxFailedAttempts)
        {
            LockoutEndUtc = utcNow.Add(LockoutDuration);
            AccessFailedCount = 0;
        }
    }

    public RefreshToken IssueRefreshToken(string tokenHash, DateTime expiresOnUtc, string? createdByIp, string? userAgent)
    {
        var token = RefreshToken.Create(Id, tokenHash, expiresOnUtc, createdByIp, userAgent);
        _refreshTokens.Add(token);
        return token;
    }

    public void RevokeAllRefreshTokens(string reason)
    {
        foreach (var token in _refreshTokens.Where(t => t.IsActive(DateTime.UtcNow)))
        {
            token.Revoke(reason, null);
        }
    }
}
