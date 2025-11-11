using System.Security.Cryptography;

namespace Api.Services;

/// <summary>
/// Service for password and secret hashing using PBKDF2.
/// Centralizes cryptographic hashing logic used across authentication services.
/// </summary>
public interface IPasswordHashingService
{
    /// <summary>
    /// Hashes a secret (password, API key, backup code) using PBKDF2 with SHA256 using default iterations.
    /// </summary>
    /// <param name="secret">The secret to hash</param>
    /// <returns>Versioned hash string in format: v1.{iterations}.{base64Salt}.{base64Hash}</returns>
    string HashSecret(string secret);

    /// <summary>
    /// Hashes a secret (password, API key, backup code) using PBKDF2 with SHA256 with custom iterations.
    /// </summary>
    /// <param name="secret">The secret to hash</param>
    /// <param name="iterations">Number of PBKDF2 iterations</param>
    /// <returns>Versioned hash string in format: v1.{iterations}.{base64Salt}.{base64Hash}</returns>
    string HashSecret(string secret, int iterations);

    /// <summary>
    /// Verifies a secret against a stored hash using constant-time comparison.
    /// </summary>
    /// <param name="secret">The secret to verify</param>
    /// <param name="storedHash">The stored hash to compare against</param>
    /// <returns>True if the secret matches the hash</returns>
    bool VerifySecret(string secret, string storedHash);

    /// <summary>
    /// Default PBKDF2 iteration count (210,000 as per OWASP recommendations for 2024)
    /// </summary>
    const int DefaultIterations = 210_000;
}

/// <summary>
/// Implementation of password hashing service using PBKDF2-HMAC-SHA256.
/// </summary>
public class PasswordHashingService : IPasswordHashingService
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    /// <inheritdoc />
    public string HashSecret(string secret)
    {
        return HashSecret(secret, IPasswordHashingService.DefaultIterations);
    }

    /// <inheritdoc />
    public string HashSecret(string secret, int iterations)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(iterations, 0);

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            secret,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSizeBytes
        );

        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc />
    public bool VerifySecret(string secret, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedHash);

        try
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 4 || parts[0] != "v1")
            {
                return false;
            }

            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var hash = Rfc2898DeriveBytes.Pbkdf2(
                secret,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length
            );

            return CryptographicOperations.FixedTimeEquals(hash, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
