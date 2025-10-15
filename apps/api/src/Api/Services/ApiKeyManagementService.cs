using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for managing API keys: CRUD operations, rotation, and quota management.
/// Builds on top of ApiKeyAuthenticationService to provide full lifecycle management.
/// Related to Issue #259 - API-04: API Key Management and Quota System.
/// </summary>
public class ApiKeyManagementService
{
    private readonly MeepleAiDbContext _db;
    private readonly ApiKeyAuthenticationService _authService;
    private readonly ILogger<ApiKeyManagementService> _logger;
    private readonly TimeProvider _timeProvider;

    public ApiKeyManagementService(
        MeepleAiDbContext db,
        ApiKeyAuthenticationService authService,
        ILogger<ApiKeyManagementService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _authService = authService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Lists all API keys for a specific user.
    /// </summary>
    /// <param name="userId">ID of the user whose keys to list</param>
    /// <param name="includeRevoked">Whether to include revoked keys</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of API keys</returns>
    public async Task<ApiKeyListResponse> ListApiKeysAsync(
        string userId,
        bool includeRevoked = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.ApiKeys.Where(k => k.UserId == userId);

        if (!includeRevoked)
        {
            query = query.Where(k => k.RevokedAt == null);
        }

        var totalCount = await query.CountAsync(ct);

        var keys = await query
            .OrderByDescending(k => k.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => MapToDto(k))
            .ToListAsync(ct);

        return new ApiKeyListResponse
        {
            Keys = keys,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Gets a specific API key by ID.
    /// </summary>
    /// <param name="keyId">ID of the key to retrieve</param>
    /// <param name="userId">ID of the user who owns the key (for authorization)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>API key DTO or null if not found/unauthorized</returns>
    public async Task<ApiKeyDto?> GetApiKeyAsync(string keyId, string userId, CancellationToken ct = default)
    {
        var apiKey = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct);

        return apiKey == null ? null : MapToDto(apiKey);
    }

    /// <summary>
    /// Creates a new API key for a user.
    /// </summary>
    /// <param name="userId">ID of the user who will own the key</param>
    /// <param name="request">API key creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Response containing the new key and plaintext value</returns>
    public async Task<CreateApiKeyResponse> CreateApiKeyAsync(
        string userId,
        CreateApiKeyRequest request,
        CancellationToken ct = default)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.KeyName))
            throw new ArgumentException("Key name is required", nameof(request));

        // Generate the API key using the authentication service
        var (plaintextKey, entity) = await _authService.GenerateApiKeyAsync(
            userId,
            request.KeyName,
            request.Scopes,
            request.ExpiresAt,
            request.Environment,
            ct);

        // Add quota information as metadata if specified
        if (request.MaxRequestsPerDay.HasValue || request.MaxRequestsPerHour.HasValue)
        {
            var quota = new
            {
                maxRequestsPerDay = request.MaxRequestsPerDay,
                maxRequestsPerHour = request.MaxRequestsPerHour
            };
            entity.Metadata = System.Text.Json.JsonSerializer.Serialize(quota);
        }

        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "API key created. KeyId: {KeyId}, UserId: {UserId}, Name: {Name}",
            entity.Id,
            userId,
            request.KeyName);

        return new CreateApiKeyResponse
        {
            ApiKey = MapToDto(entity),
            PlaintextKey = plaintextKey
        };
    }

    /// <summary>
    /// Updates an existing API key.
    /// </summary>
    /// <param name="keyId">ID of the key to update</param>
    /// <param name="userId">ID of the user who owns the key (for authorization)</param>
    /// <param name="request">Update request with fields to change</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated API key DTO or null if not found/unauthorized</returns>
    public async Task<ApiKeyDto?> UpdateApiKeyAsync(
        string keyId,
        string userId,
        UpdateApiKeyRequest request,
        CancellationToken ct = default)
    {
        var apiKey = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct);

        if (apiKey == null)
        {
            _logger.LogWarning("API key update failed: key not found or unauthorized. KeyId: {KeyId}", keyId);
            return null;
        }

        // Update fields if provided
        if (request.KeyName != null)
            apiKey.KeyName = request.KeyName;

        if (request.Scopes != null)
            apiKey.Scopes = request.Scopes;

        if (request.ExpiresAt.HasValue)
            apiKey.ExpiresAt = request.ExpiresAt.Value;

        if (request.IsActive.HasValue)
            apiKey.IsActive = request.IsActive.Value;

        // Update quota metadata if specified
        if (request.MaxRequestsPerDay.HasValue || request.MaxRequestsPerHour.HasValue)
        {
            var quota = new
            {
                maxRequestsPerDay = request.MaxRequestsPerDay,
                maxRequestsPerHour = request.MaxRequestsPerHour
            };
            apiKey.Metadata = System.Text.Json.JsonSerializer.Serialize(quota);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("API key updated. KeyId: {KeyId}, UserId: {UserId}", keyId, userId);

        return MapToDto(apiKey);
    }

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    /// <param name="keyId">ID of the key to revoke</param>
    /// <param name="userId">ID of the user performing the revocation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if revoked successfully, false if not found or unauthorized</returns>
    public async Task<bool> RevokeApiKeyAsync(string keyId, string userId, CancellationToken ct = default)
    {
        // Check that the user owns the key before revoking
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct);
        if (apiKey == null)
        {
            _logger.LogWarning("API key revocation failed: key not found or unauthorized. KeyId: {KeyId}, UserId: {UserId}", keyId, userId);
            return false;
        }

        return await _authService.RevokeApiKeyAsync(keyId, userId, ct);
    }

    /// <summary>
    /// Rotates an API key by creating a new key and revoking the old one.
    /// The new key inherits the scopes and settings from the old key.
    /// </summary>
    /// <param name="keyId">ID of the key to rotate</param>
    /// <param name="userId">ID of the user who owns the key (for authorization)</param>
    /// <param name="request">Rotation request with optional new settings</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Response containing the new key or null if not found/unauthorized</returns>
    public async Task<RotateApiKeyResponse?> RotateApiKeyAsync(
        string keyId,
        string userId,
        RotateApiKeyRequest request,
        CancellationToken ct = default)
    {
        var oldKey = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct);

        if (oldKey == null)
        {
            _logger.LogWarning("API key rotation failed: key not found or unauthorized. KeyId: {KeyId}", keyId);
            return null;
        }

        // Extract environment from old key prefix
        var environment = oldKey.KeyPrefix.Contains("live") ? "live" : "test";

        // Generate new key with same scopes and settings
        var (plaintextKey, newEntity) = await _authService.GenerateApiKeyAsync(
            userId,
            $"{oldKey.KeyName} (Rotated)",
            oldKey.Scopes,
            request.ExpiresAt ?? oldKey.ExpiresAt,
            environment,
            ct);

        // Copy quota metadata from old key
        newEntity.Metadata = oldKey.Metadata;

        _db.ApiKeys.Add(newEntity);

        // Revoke old key
        await _authService.RevokeApiKeyAsync(keyId, userId, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "API key rotated. OldKeyId: {OldKeyId}, NewKeyId: {NewKeyId}, UserId: {UserId}",
            keyId,
            newEntity.Id,
            userId);

        return new RotateApiKeyResponse
        {
            NewApiKey = MapToDto(newEntity),
            PlaintextKey = plaintextKey,
            RevokedKeyId = keyId
        };
    }

    /// <summary>
    /// Deletes an API key permanently (admin only operation).
    /// </summary>
    /// <param name="keyId">ID of the key to delete</param>
    /// <param name="userId">ID of the user performing the deletion</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if deleted successfully, false if not found</returns>
    public async Task<bool> DeleteApiKeyAsync(string keyId, string userId, CancellationToken ct = default)
    {
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId, ct);
        if (apiKey == null)
        {
            _logger.LogWarning("API key deletion failed: key not found. KeyId: {KeyId}", keyId);
            return false;
        }

        _db.ApiKeys.Remove(apiKey);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("API key deleted. KeyId: {KeyId}, DeletedBy: {DeletedBy}", keyId, userId);

        return true;
    }

    /// <summary>
    /// Gets usage statistics for an API key.
    /// </summary>
    /// <param name="keyId">ID of the key</param>
    /// <param name="userId">ID of the user who owns the key (for authorization)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Usage statistics or null if not found/unauthorized</returns>
    public async Task<ApiKeyQuotaDto?> GetApiKeyUsageAsync(
        string keyId,
        string userId,
        CancellationToken ct = default)
    {
        var apiKey = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct);

        if (apiKey == null)
            return null;

        // Parse quota from metadata
        var quota = ParseQuotaFromMetadata(apiKey.Metadata);

        // TODO: Implement actual usage tracking from request logs
        // For now, return placeholder data
        return new ApiKeyQuotaDto
        {
            MaxRequestsPerDay = quota.MaxRequestsPerDay,
            MaxRequestsPerHour = quota.MaxRequestsPerHour,
            RequestsToday = 0,
            RequestsThisHour = 0,
            ResetsAt = DateTime.UtcNow.Date.AddDays(1)
        };
    }

    #region Helper Methods

    private static ApiKeyDto MapToDto(ApiKeyEntity entity)
    {
        var quota = ParseQuotaFromMetadata(entity.Metadata);

        return new ApiKeyDto
        {
            Id = entity.Id,
            KeyName = entity.KeyName,
            KeyPrefix = entity.KeyPrefix,
            Scopes = entity.Scopes,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            LastUsedAt = entity.LastUsedAt,
            ExpiresAt = entity.ExpiresAt,
            RevokedAt = entity.RevokedAt,
            RevokedBy = entity.RevokedBy,
            Quota = quota.MaxRequestsPerDay.HasValue || quota.MaxRequestsPerHour.HasValue
                ? new ApiKeyQuotaDto
                {
                    MaxRequestsPerDay = quota.MaxRequestsPerDay,
                    MaxRequestsPerHour = quota.MaxRequestsPerHour,
                    RequestsToday = 0,
                    RequestsThisHour = 0,
                    ResetsAt = DateTime.UtcNow.Date.AddDays(1)
                }
                : null
        };
    }

    private static (int? MaxRequestsPerDay, int? MaxRequestsPerHour) ParseQuotaFromMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return (null, null);

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadata);
            var root = doc.RootElement;

            var maxPerDay = root.TryGetProperty("maxRequestsPerDay", out var dayProp) && dayProp.ValueKind == System.Text.Json.JsonValueKind.Number
                ? (int?)dayProp.GetInt32()
                : null;

            var maxPerHour = root.TryGetProperty("maxRequestsPerHour", out var hourProp) && hourProp.ValueKind == System.Text.Json.JsonValueKind.Number
                ? (int?)hourProp.GetInt32()
                : null;

            return (maxPerDay, maxPerHour);
        }
        catch
        {
            return (null, null);
        }
    }

    #endregion
}
