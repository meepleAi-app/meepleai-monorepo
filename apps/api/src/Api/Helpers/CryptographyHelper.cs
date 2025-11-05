using System.Security.Cryptography;
using System.Text;

namespace Api.Helpers;

/// <summary>
/// Centralized cryptographic utilities to eliminate code duplication across services.
/// Provides standardized hashing methods for consistent security operations.
/// </summary>
public static class CryptographyHelper
{
    /// <summary>
    /// Computes SHA256 hash of the input string and returns lowercase hexadecimal representation.
    /// Used for cache keys, session tokens, and other non-password hashing needs.
    /// </summary>
    /// <param name="input">The string to hash</param>
    /// <returns>Lowercase hexadecimal SHA256 hash</returns>
    /// <remarks>
    /// DO NOT use for password hashing - use PasswordHashingService (PBKDF2) instead.
    /// This method is suitable for:
    /// - Cache key generation
    /// - Session token hashing
    /// - CSRF state token hashing
    /// - API request signatures
    /// - Non-sensitive data fingerprinting
    /// </remarks>
    public static string ComputeSha256Hash(string input)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);

        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA256 hash of byte array and returns lowercase hexadecimal representation.
    /// Useful for file integrity checks and binary data hashing.
    /// </summary>
    /// <param name="bytes">The byte array to hash</param>
    /// <returns>Lowercase hexadecimal SHA256 hash</returns>
    public static string ComputeSha256Hash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA256 hash of the input string and returns Base64 representation.
    /// Used for session tokens and other scenarios requiring Base64 encoding.
    /// </summary>
    /// <param name="input">The string to hash</param>
    /// <returns>Base64-encoded SHA256 hash</returns>
    /// <remarks>
    /// Use this for:
    /// - Session token hashing (maintains backward compatibility with existing tokens)
    /// - Scenarios where Base64 encoding is required for transport or storage
    ///
    /// Use ComputeSha256Hash (hex) for:
    /// - Cache keys (shorter representation)
    /// - New implementations without legacy compatibility requirements
    /// </remarks>
    public static string ComputeSha256HashBase64(string input)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);

        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Computes SHA256 hash and returns raw byte array.
    /// Useful for cryptographic operations that require raw hash bytes.
    /// </summary>
    /// <param name="input">The string to hash</param>
    /// <returns>Raw SHA256 hash bytes (32 bytes)</returns>
    /// <remarks>
    /// Use this for:
    /// - Encryption key derivation
    /// - HMAC operations
    /// - Other cryptographic primitives requiring raw bytes
    /// </remarks>
    public static byte[] ComputeSha256HashBytes(string input)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);

        var bytes = Encoding.UTF8.GetBytes(input);
        return SHA256.HashData(bytes);
    }
}
