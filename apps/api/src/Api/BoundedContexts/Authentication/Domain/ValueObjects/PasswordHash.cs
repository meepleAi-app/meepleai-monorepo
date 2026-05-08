using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using System;
using System.Diagnostics.CodeAnalysis;
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

    // I7 (auth security fixes): PBKDF2-SHA256 iterations bumped from 210k
    // (OWASP 2021) to 600k (OWASP 2023). Applied only to NEW hashes —
    // existing 210k hashes continue to verify because VerifyVersionedHash
    // reads the iteration count from the stored value.
    private const int Iterations = 600_000;

    // I7: minimum password length raised from 8 to 12. Modern guidance
    // (NIST SP 800-63B Rev 4) prioritises length over composition rules;
    // 12 chars makes brute-force pricing meaningfully harder for any
    // policy-compliant password.
    [SuppressMessage("SonarAnalyzer.CSharp", "S125", Justification = "Explanatory comment about NIST SP 800-63B Rev 4 password guidance, false-positive on parenthetical citation.")]
    private const int MinPasswordLength = 12;

    // I7: maximum password length capped at 128 to prevent
    // resource-exhaustion attacks via extremely long inputs through
    // PBKDF2 (which would otherwise scale linearly with input size).
    // 128 chars is well above any sensible passphrase.
    private const int MaxPasswordLength = 128;

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

        // I7: length bounds. Min raised from 8 to 12 (NIST SP 800-63B Rev 4).
        // Max capped at 128 to bound PBKDF2 input cost.
        if (plaintextPassword.Length < MinPasswordLength)
            throw new ValidationException(
                nameof(PasswordHash),
                $"Password must be at least {MinPasswordLength} characters");

        if (plaintextPassword.Length > MaxPasswordLength)
            throw new ValidationException(
                nameof(PasswordHash),
                $"Password cannot exceed {MaxPasswordLength} characters");

        // I7: top-N common-password blocklist. Rejects passwords that are
        // length-compliant but trivially guessable (e.g. "password1234",
        // "qwerty123456", "Password123!"). Lookup is O(1) via HashSet.
        if (CommonPasswordBlocklist.IsCommon(plaintextPassword))
            throw new ValidationException(
                nameof(PasswordHash),
                "Password is in the list of well-known compromised passwords. Choose a less common password.");

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

    // R1 (auth security fixes): implicit string conversion removed. Same
    // reasoning as SessionToken: the operator silently bypassed the
    // "[REDACTED]" guard whenever a PasswordHash landed in a string-typed
    // context (logging, string interpolation). Callers must reach for
    // .Value explicitly so any path that exposes the raw stored hash is
    // visible at the call site.

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
