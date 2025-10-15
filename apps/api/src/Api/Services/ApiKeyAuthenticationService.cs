using System.Security.Cryptography;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for API key authentication and validation.
/// Provides methods to validate API keys, generate new keys, and manage key lifecycle.
/// Uses PBKDF2 with SHA256 for key hashing, consistent with the existing password hashing strategy.
/// </summary>
public class ApiKeyAuthenticationService
{
    private const string KeyPrefix = "mpl";
    private const int KeyLengthBytes = 32; // 256 bits = 32 bytes
    private const int HashIterations = 210_000; // Same as password hashing for consistency
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<ApiKeyAuthenticationService> _logger;
    private readonly TimeProvider _timeProvider;

    public ApiKeyAuthenticationService(
        MeepleAiDbContext db,
        ILogger<ApiKeyAuthenticationService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Validates an API key and returns validation result with user information.
    /// Performs security checks: key exists, is active, not expired, not revoked.
    /// Updates last_used_at timestamp asynchronously (fire-and-forget).
    /// </summary>
    /// <param name="apiKey">The full API key to validate (e.g., "mpl_live_...")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with user details if valid, or invalid result with reason</returns>
    public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("API key validation failed: empty or null key");
            return ApiKeyValidationResult.Invalid("API key is required");
        }

        // Extract prefix for logging (e.g., "mpl_xxxx")
        var displayPrefix = ExtractDisplayPrefix(apiKey);

        // Query active keys with user information
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var candidateKeys = await _db.ApiKeys
            .Include(k => k.User)
            .Where(k =>
                k.IsActive &&
                (k.ExpiresAt == null || k.ExpiresAt > now) &&
                k.RevokedAt == null)
            .ToListAsync(ct);

        // Find key by verifying hash (need to extract salt from stored hash)
        var apiKeyEntity = candidateKeys.FirstOrDefault(k => VerifyApiKey(apiKey, k.KeyHash));

        if (apiKeyEntity == null)
        {
            _logger.LogWarning("API key validation failed: key not found, inactive, expired, or revoked. Prefix: {Prefix}", displayPrefix);
            return ApiKeyValidationResult.Invalid("Invalid, expired, or revoked API key");
        }

        // Update last used timestamp (fire-and-forget, non-blocking)
        _ = UpdateLastUsedAsync(apiKeyEntity.Id);

        _logger.LogInformation(
            "API key validated successfully. KeyId: {KeyId}, UserId: {UserId}, Scopes: {Scopes}",
            apiKeyEntity.Id,
            apiKeyEntity.UserId,
            string.Join(",", apiKeyEntity.Scopes));

        return ApiKeyValidationResult.Valid(
            apiKeyEntity.Id,
            apiKeyEntity.User.Id,
            apiKeyEntity.User.Email,
            apiKeyEntity.User.DisplayName,
            apiKeyEntity.User.Role.ToString(),
            apiKeyEntity.Scopes);
    }

    /// <summary>
    /// Generates a new API key for a user.
    /// Returns both the full plaintext key (only shown once) and the entity to save.
    /// The key format is: mpl_{environment}_{random_base64}
    /// </summary>
    /// <param name="userId">ID of the user who owns the key</param>
    /// <param name="keyName">Human-readable name for the key</param>
    /// <param name="scopes">Permission scopes for the key</param>
    /// <param name="expiresAt">Optional expiration date</param>
    /// <param name="environment">Environment prefix (live/test)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple of (plaintext key, key entity)</returns>
    public async Task<(string PlaintextKey, ApiKeyEntity Entity)> GenerateApiKeyAsync(
        string userId,
        string keyName,
        string[] scopes,
        DateTime? expiresAt = null,
        string environment = "live",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name is required", nameof(keyName));
        if (!new[] { "live", "test" }.Contains(environment))
            throw new ArgumentException("Environment must be 'live' or 'test'", nameof(environment));

        // Verify user exists
        var user = await _db.Users.FindAsync([userId], ct);
        if (user == null)
            throw new InvalidOperationException($"User not found: {userId}");

        // Generate random key bytes
        var keyBytes = RandomNumberGenerator.GetBytes(KeyLengthBytes);
        var keySecret = Convert.ToBase64String(keyBytes).Replace("+", "").Replace("/", "").Replace("=", "");

        // Format: mpl_live_... or mpl_test_...
        var plaintextKey = $"{KeyPrefix}_{environment}_{keySecret}";

        // Extract prefix for display (first 8 chars after mpl_)
        var displayPrefix = ExtractDisplayPrefix(plaintextKey);

        // Hash the key for storage
        var keyHash = HashApiKey(plaintextKey);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            KeyName = keyName,
            KeyHash = keyHash,
            KeyPrefix = displayPrefix,
            Scopes = scopes,
            IsActive = true,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            User = user
        };

        _logger.LogInformation(
            "Generated new API key. KeyId: {KeyId}, UserId: {UserId}, Name: {Name}, Scopes: {Scopes}",
            entity.Id,
            userId,
            keyName,
            string.Join(",", scopes));

        return (plaintextKey, entity);
    }

    /// <summary>
    /// Revokes an API key, marking it as revoked.
    /// </summary>
    /// <param name="keyId">ID of the key to revoke</param>
    /// <param name="revokedByUserId">ID of the user performing the revocation</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<bool> RevokeApiKeyAsync(string keyId, string revokedByUserId, CancellationToken ct = default)
    {
        var apiKey = await _db.ApiKeys.FindAsync([keyId], ct);
        if (apiKey == null)
        {
            _logger.LogWarning("API key revocation failed: key not found. KeyId: {KeyId}", keyId);
            return false;
        }

        if (apiKey.RevokedAt != null)
        {
            _logger.LogInformation("API key already revoked. KeyId: {KeyId}", keyId);
            return true;
        }

        apiKey.RevokedAt = _timeProvider.GetUtcNow().UtcDateTime;
        apiKey.RevokedBy = revokedByUserId;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "API key revoked. KeyId: {KeyId}, RevokedBy: {RevokedBy}",
            keyId,
            revokedByUserId);

        return true;
    }

    /// <summary>
    /// Updates the last used timestamp for an API key.
    /// Fire-and-forget operation that doesn't block the request.
    /// </summary>
    private async Task UpdateLastUsedAsync(string keyId)
    {
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            await _db.ApiKeys
                .Where(k => k.Id == keyId)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now));

            _logger.LogDebug("Updated last_used_at for API key {KeyId}", keyId);
        }
        catch (Exception ex)
        {
            // Don't throw - this is a fire-and-forget operation
            _logger.LogError(ex, "Failed to update last_used_at for API key {KeyId}", keyId);
        }
    }

    /// <summary>
    /// Hashes an API key using PBKDF2 with SHA256.
    /// Format: v1.{iterations}.{base64_salt}.{base64_hash}
    /// Same approach as password hashing for consistency.
    /// </summary>
    private static string HashApiKey(string apiKey)
    {
        const int iterations = HashIterations;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(apiKey, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies an API key against a stored hash.
    /// Extracts the salt from the stored hash and recomputes the hash to compare.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    /// <param name="apiKey">The plaintext API key to verify</param>
    /// <param name="encodedHash">The stored hash in format v1.{iterations}.{salt}.{hash}</param>
    /// <returns>True if the key matches the hash, false otherwise</returns>
    private static bool VerifyApiKey(string apiKey, string encodedHash)
    {
        try
        {
            var parts = encodedHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || parts[0] != "v1")
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var iterations))
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var hash = Rfc2898DeriveBytes.Pbkdf2(apiKey, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
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

    /// <summary>
    /// Extracts a display prefix from an API key for identification.
    /// Returns the first 8 characters after the environment prefix.
    /// Example: "mpl_live_abc123xyz..." -> "mpl_abc1"
    /// </summary>
    private static string ExtractDisplayPrefix(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "mpl_????";

        // Format is: mpl_{env}_{secret}
        var parts = apiKey.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[0] == KeyPrefix)
        {
            // Take first 4 chars of the secret part
            var secret = parts[2];
            return $"{KeyPrefix}_{secret[..Math.Min(4, secret.Length)]}";
        }

        // Fallback: just take first 8 chars
        return apiKey.Length > 8 ? apiKey[..8] : apiKey;
    }
}
