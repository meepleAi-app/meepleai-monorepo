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
internal class ApiKeyAuthenticationService
{
    private const string KeyPrefix = "mpl";
    private const int KeyLengthBytes = 32; // 256 bits = 32 bytes
    private readonly MeepleAiDbContext _db;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly ILogger<ApiKeyAuthenticationService> _logger;
    private readonly TimeProvider _timeProvider;

    public ApiKeyAuthenticationService(
        MeepleAiDbContext db,
        IPasswordHashingService passwordHashingService,
        ILogger<ApiKeyAuthenticationService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _passwordHashingService = passwordHashingService;
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
            .AsNoTracking() // PERF-05: Read-only query for authentication validation
            .Include(k => k.User)
            .Where(k =>
                k.IsActive &&
                (k.ExpiresAt == null || k.ExpiresAt > now) &&
                k.RevokedAt == null &&
                k.KeyPrefix == displayPrefix)
            .ToListAsync(ct).ConfigureAwait(false);

        // Find key by verifying hash (need to extract salt from stored hash)
        var apiKeyEntity = candidateKeys.FirstOrDefault(k => VerifyApiKey(apiKey, k.KeyHash));

        if (apiKeyEntity == null)
        {
            _logger.LogWarning("API key validation failed: key not found, inactive, expired, or revoked. Prefix: {Prefix}", displayPrefix);
            return ApiKeyValidationResult.Invalid("Invalid, expired, or revoked API key");
        }

        if (apiKeyEntity.User == null)
        {
            _logger.LogError("API key found but user entity not loaded. KeyId: {KeyId}", apiKeyEntity.Id);
            return ApiKeyValidationResult.Invalid("Invalid API key configuration");
        }

        // Update last used timestamp (fire-and-forget, non-blocking)
        _ = UpdateLastUsedAsync(apiKeyEntity.Id.ToString());

        _logger.LogInformation(
            "API key validated successfully. KeyId: {KeyId}, UserId: {UserId}, Scopes: {Scopes}",
            apiKeyEntity.Id.ToString(),
            apiKeyEntity.UserId.ToString(),
            apiKeyEntity.Scopes);

        return ApiKeyValidationResult.Valid(
            apiKeyEntity.Id.ToString(),
            apiKeyEntity.User.Id.ToString(),
            apiKeyEntity.User.Email,
            apiKeyEntity.User.DisplayName,
            apiKeyEntity.User.Role,
            apiKeyEntity.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Generates a new API key for a user.
    /// Returns both the full plaintext key (only shown once) and the entity to save.
    /// The key format is: mpl_{environment}_{random_base64}
    /// </summary>
    /// <returns>Tuple of (plaintext key, key entity)</returns>
    private static readonly string[] ValidEnvironments = { "live", "test" };

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
        if (!ValidEnvironments.Contains(environment, StringComparer.Ordinal))
            throw new ArgumentException("Environment must be 'live' or 'test'", nameof(environment));

        // Verify user exists
        if (!Guid.TryParse(userId, out var userGuid))
            throw new ArgumentException("User ID must be a valid GUID", nameof(userId));

        var user = await _db.Users.FindAsync([userGuid], ct).ConfigureAwait(false);
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
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(userId),
            KeyName = keyName,
            KeyHash = keyHash,
            KeyPrefix = displayPrefix,
            Scopes = string.Join(",", scopes),
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
        if (!Guid.TryParse(keyId, out var keyGuid))
        {
            _logger.LogWarning("API key revocation failed: invalid key format {KeyId}", keyId);
            return false;
        }

        var apiKey = await _db.ApiKeys.FindAsync([keyGuid], ct).ConfigureAwait(false);
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
        apiKey.RevokedBy = revokedByUserId != null && Guid.TryParse(revokedByUserId, out var revokedByGuid) ? revokedByGuid : null;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

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
            if (!Guid.TryParse(keyId, out var keyGuid))
            {
                _logger.LogError("Invalid API key ID format: {KeyId}", keyId);
                return;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            await _db.ApiKeys
                .Where(k => k.Id == keyGuid)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now)).ConfigureAwait(false);

            _logger.LogDebug("Updated last_used_at for API key {KeyId}", keyId);
        }
        catch (DbUpdateException ex)
        {
            // FIRE-AND-FORGET PATTERN: last_used_at update must not fail authentication
            // Rationale: Updating the last_used_at timestamp is a telemetry operation that
            // tracks API key usage patterns. Authentication already succeeded - failing the
            // request because we cannot update metadata violates fail-safe principles.
            // Context: DB failures are typically transient (connection loss, lock timeout)
            _logger.LogError(ex, "Database error updating last_used_at for API key {KeyId}", keyId);
        }
        catch (InvalidOperationException ex)
        {
            // FIRE-AND-FORGET PATTERN: last_used_at update must not fail authentication
            // Rationale: InvalidOperationException indicates context/DbSet disposal during
            // async execution. Authentication succeeded, metadata update is best-effort only.
            // Context: Typically occurs during high load or aggressive DbContext pooling
            _logger.LogError(ex, "Invalid operation updating last_used_at for API key {KeyId}", keyId);
        }
    }

    /// <summary>
    /// Hashes an API key using centralized IPasswordHashingService
    /// </summary>
    private string HashApiKey(string apiKey)
    {
        return _passwordHashingService.HashSecret(apiKey);
    }

    /// <summary>
    /// Verifies an API key against a stored hash using centralized IPasswordHashingService
    /// </summary>
    private bool VerifyApiKey(string apiKey, string encodedHash)
    {
        return _passwordHashingService.VerifySecret(apiKey, encodedHash);
    }

    /// <summary>
    /// Extracts a display prefix from an API key for identification.
    /// Returns the first 8 characters after the environment prefix.
    /// Example: "mpl_live_abc123xyz..." -> "mpl_abc1"
    /// </summary>
    private static string ExtractDisplayPrefix(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "mpl_???";

        // Format is: mpl_{env}_{secret}
        var parts = apiKey.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && string.Equals(parts[0], KeyPrefix, StringComparison.Ordinal))
        {
            // Take first 4 chars of the secret part
            var secret = parts[2];
            return $"{KeyPrefix}_{secret[..Math.Min(4, secret.Length)]}";
        }

        // Fallback: just take first 8 chars
        return apiKey.Length > 8 ? apiKey[..8] : apiKey;
    }
}
