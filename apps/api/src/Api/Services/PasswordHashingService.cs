using System;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Service for password and secret hashing using PBKDF2.
/// Centralizes cryptographic hashing logic used across authentication services.
/// </summary>
internal interface IPasswordHashingService
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
internal class PasswordHashingService : IPasswordHashingService
{
    private const string HashVersion = "v1";
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
        // Use explicit UTF-8 encoding to match PasswordHash domain value object
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(secret),
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSizeBytes
        );

        return $"{HashVersion}.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc />
    public bool VerifySecret(string secret, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedHash);

        try
        {
            if (TryParseVersionedHash(storedHash, out var iterations, out var salt, out var expected))
            {
                var computed = ComputeHash(secret, salt, iterations, expected.Length);
                return CryptographicOperations.FixedTimeEquals(computed, expected);
            }

            return false;
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

    private static byte[] ComputeHash(string secret, byte[] salt, int iterations, int outputLength)
    {
        // Use explicit UTF-8 encoding to match PasswordHash domain value object
        return Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(secret),
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: outputLength
        );
    }

    private static bool TryParseVersionedHash(string storedHash, out int iterations, out byte[] salt, out byte[] expectedHash)
    {
        iterations = default;
        salt = Array.Empty<byte>();
        expectedHash = Array.Empty<byte>();

        var parts = storedHash.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !parts[0].Equals(HashVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out iterations))
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

}
