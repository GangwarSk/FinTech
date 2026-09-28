using FinanceAudit360.Domain.Common;

namespace FinanceAudit360.Domain.Entities;

/// <summary>
/// Refresh tokens are stored as a SHA-256 hash only; the plaintext never touches the database.
/// Rotation is enforced by linking the replacement token.
/// </summary>
public class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    private RefreshToken(Guid userId, string tokenHash, DateTime expiresOnUtc, string? createdByIp, string? userAgent)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresOnUtc = expiresOnUtc;
        CreatedOnUtc = DateTime.UtcNow;
        CreatedByIp = createdByIp;
        UserAgent = userAgent;
    }

    public Guid UserId { get; private set; }

    public User? User { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTime? RevokedOnUtc { get; private set; }

    public string? RevokedReason { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresOnUtc;

    public bool IsActive(DateTime utcNow) => RevokedOnUtc is null && !IsExpired(utcNow);

    internal static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresOnUtc, string? createdByIp, string? userAgent)
        => new(userId, tokenHash, expiresOnUtc, createdByIp, userAgent);

    public void Revoke(string reason, string? replacedByTokenHash)
    {
        if (RevokedOnUtc is not null)
        {
            return;
        }

        RevokedOnUtc = DateTime.UtcNow;
        RevokedReason = reason;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
