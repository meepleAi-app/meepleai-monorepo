using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using System.Security.Cryptography;
using System.Text;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Represents a hashed password value object.
/// Uses PBKDF2 with 210,000 iterations for secure password hashing.
/// </summary>
public sealed class PasswordHash : ValueObject
{
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

        // Generate random salt
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        // Derive key using PBKDF2
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(plaintextPassword),
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSize
        );

        // Store as: salt:hash (both base64 encoded)
        var storedHash = $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
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

        try
        {
            // Parse stored hash
            var parts = Value.Split(':');
            if (parts.Length != 2)
                return false;

            var salt = Convert.FromBase64String(parts[0]);
            var storedHash = Convert.FromBase64String(parts[1]);

            // Recompute hash with same salt
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.UTF8.GetBytes(plaintextPassword),
                salt: salt,
                iterations: Iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: HashSize
            );

            // Constant-time comparison (prevent timing attacks)
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => "[REDACTED]"; // Never expose hash value

    public static implicit operator string(PasswordHash hash) => hash.Value;
}
