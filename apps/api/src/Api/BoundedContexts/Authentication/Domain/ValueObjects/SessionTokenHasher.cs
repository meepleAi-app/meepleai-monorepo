using System.Security.Cryptography;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Centralized session token hashing.
/// Single source of truth: storage and validation must use identical hash computation.
/// Resolves C1: hash mismatch between SessionToken.ComputeHash and inline endpoint hashing.
/// </summary>
public static class SessionTokenHasher
{
    private const int MaxDecodedBytes = 256;

    /// <summary>
    /// Hash a session token from cookie value.
    /// Throws ValidationException if token format is invalid (not Base-64).
    /// </summary>
    /// <param name="cookieValue">Raw cookie value (Base-64 encoded random bytes).</param>
    /// <returns>SHA-256 hash of the decoded token bytes, encoded as Base-64.</returns>
    /// <exception cref="ValidationException">Thrown when cookieValue is null, empty, or not valid Base-64.</exception>
    public static string HashFromCookie(string cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
            throw new ValidationException(nameof(cookieValue), "Session token cannot be empty");

        // Validate Base-64 format and decode in a single pass.
        // Real tokens are 44 chars (32 bytes Base-64-encoded); MaxDecodedBytes is well above that.
        Span<byte> tokenBytes = stackalloc byte[MaxDecodedBytes];
        if (cookieValue.Length > MaxDecodedBytes * 4 / 3 + 4 ||
            !Convert.TryFromBase64String(cookieValue, tokenBytes, out int bytesWritten))
        {
            throw new ValidationException(nameof(cookieValue), "Session token is not a valid format");
        }

        // SHA-256 of decoded raw bytes (matches Session.TokenHash storage).
        var hashBytes = SHA256.HashData(tokenBytes[..bytesWritten]);
        return Convert.ToBase64String(hashBytes);
    }
}
