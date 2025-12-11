using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Represents a hashed password value object.
/// Uses PBKDF2 with 210,000 iterations for secure password hashing.
/// </summary>
public sealed class PasswordHash : ValueObject
{
    private const string HashVersion = "v1";
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 210_000; // OWASP recommendation for PBKDF2-SHA256

    public string Value { get; }

    /// <summary>
    /// Private constructor for reconstructing from stored hash.
    /// </summary>
    private PasswordHash(string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            throw new ValidationException(nameof(PasswordHash), "Password hash cannot be empty");

        Value = storedHash;
    }

    /// <summary>
    /// Creates a new password hash from a plaintext password.
    /// </summary>
    public static PasswordHash Create(string plaintextPassword)
    {
        if (string.IsNullOrWhiteSpace(plaintextPassword))
            throw new ValidationException(nameof(PasswordHash), "Password cannot be empty");

        if (plaintextPassword.Length < 8)
            throw new ValidationException(nameof(PasswordHash), "Password must be at least 8 characters");

        var storedHash = CreateVersionedHash(plaintextPassword);
        return new PasswordHash(storedHash);
    }

    /// <summary>
    /// Reconstructs a password hash from a stored string.
    /// </summary>
    public static PasswordHash FromStored(string storedHash)
    {
        return new PasswordHash(storedHash);
    }

    /// <summary>
    /// Verifies if a plaintext password matches this hash.
    /// </summary>
    public bool Verify(string plaintextPassword)
    {
        if (string.IsNullOrWhiteSpace(plaintextPassword))
            return false;

        return VerifyVersionedHash(plaintextPassword);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => "[REDACTED]"; // Never expose hash value

    public static implicit operator string(PasswordHash hash) => hash.Value;

    private static string CreateVersionedHash(string plaintextPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(plaintextPassword),
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSize
        );

        return $"{HashVersion}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private bool VerifyVersionedHash(string plaintextPassword)
    {
        try
        {
            var parts = Value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || !parts[0].Equals(HashVersion, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations))
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);

            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.UTF8.GetBytes(plaintextPassword),
                salt: salt,
                iterations: iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: expectedHash.Length
            );

            return CryptographicOperations.FixedTimeEquals(expectedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

}
