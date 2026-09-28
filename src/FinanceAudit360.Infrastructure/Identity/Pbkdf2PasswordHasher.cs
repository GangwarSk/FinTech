using System.Security.Cryptography;
using FinanceAudit360.Application.Common.Interfaces;

namespace FinanceAudit360.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA512 with a per-password random salt. The stored format is
/// "v1.iterations.base64(salt).base64(hash)" so the work factor can be raised without
/// invalidating existing credentials.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int CurrentIterations = 210_000;
    private const string Prefix = "v1";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, HashAlgorithmName.SHA512, KeySize);

        return string.Join('.', Prefix, CurrentIterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        if (!TryParse(hash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool NeedsRehash(string hash) =>
        !TryParse(hash, out var iterations, out _, out _) || iterations < CurrentIterations;

    private static bool TryParse(string hash, out int iterations, out byte[] salt, out byte[] key)
    {
        iterations = 0;
        salt = [];
        key = [];

        var parts = hash.Split('.');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out iterations))
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            key = Convert.FromBase64String(parts[3]);
            return salt.Length > 0 && key.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
