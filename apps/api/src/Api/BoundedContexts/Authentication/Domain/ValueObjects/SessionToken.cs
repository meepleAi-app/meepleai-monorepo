using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using System.Security.Cryptography;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Represents a session token value object.
/// Generates cryptographically secure random tokens for session identification.
/// </summary>
public sealed class SessionToken : ValueObject
{
    private const int TokenSizeBytes = 32; // 256 bits

    public string Value { get; }

    /// <summary>
    /// Private constructor for reconstructing from stored token.
    /// </summary>
    private SessionToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(nameof(SessionToken), "Session token cannot be empty");

        Value = value;
    }

    /// <summary>
    /// Generates a new cryptographically secure session token.
    /// </summary>
    public static SessionToken Generate()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        var token = Convert.ToBase64String(tokenBytes);
        return new SessionToken(token);
    }

    /// <summary>
    /// Reconstructs a session token from a stored string.
    /// Throws ValidationException if value is not valid Base-64.
    /// Tokens are always generated as Base-64 (see Generate()); non-Base-64 values
    /// indicate a corrupted, legacy, or foreign cookie and are rejected at construction
    /// so callers never receive a raw FormatException from ComputeHash().
    /// </summary>
    public static SessionToken FromStored(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(nameof(SessionToken), "Session token cannot be empty");

        // Validate Base-64 format before accepting the token.
        // Use a small stack buffer; real tokens are 44 chars → 33 decoded bytes.
        // For oversized or invalid strings TryFromBase64String returns false immediately.
        const int MaxDecodedBytes = 256; // well above the 33 bytes of a real token
        Span<byte> discard = stackalloc byte[MaxDecodedBytes];
        if (value.Length > MaxDecodedBytes * 4 / 3 + 4 || !Convert.TryFromBase64String(value, discard, out _))
            throw new ValidationException(nameof(SessionToken), "Session token is not a valid format");

        return new SessionToken(value);
    }

    /// <summary>
    /// Computes SHA256 hash of this token for secure storage.
    /// Tokens should be stored hashed, not in plaintext.
    /// Delegates to <see cref="SessionTokenHasher.HashFromCookie"/> to keep a single source of truth
    /// shared with endpoints that hash the raw cookie value (C1 fix).
    /// </summary>
    public string ComputeHash()
    {
        return SessionTokenHasher.HashFromCookie(Value);
    }

    /// <summary>
    /// Verifies if this token matches a stored hash.
    /// </summary>
    public bool MatchesHash(string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            var computedHash = ComputeHash();
            return string.Equals(computedHash, storedHash, StringComparison.Ordinal);
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

    public override string ToString() => "[REDACTED]"; // Never expose token value

    // R1 (auth security fixes): implicit string conversion removed. The
    // operator made it too easy to log or interpolate a session token by
    // mistake (e.g. `$"token={sessionToken}"` would silently coerce, but
    // ToString() returns "[REDACTED]" — the implicit operator bypasses
    // that guard). Callers must now reach for .Value explicitly, which
    // makes the leak path visible at the call site.
}
