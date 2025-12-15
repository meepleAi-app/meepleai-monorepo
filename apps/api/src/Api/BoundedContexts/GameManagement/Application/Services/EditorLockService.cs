using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Issue #2055: Service for managing collaborative editor locks using Redis TTL.
/// Implements soft locking with automatic expiry for RuleSpec editing.
/// </summary>
internal interface IEditorLockService
{
    /// <summary>
    /// Attempts to acquire a lock for editing a RuleSpec.
    /// </summary>
    /// <param name="gameId">The game ID to lock.</param>
    /// <param name="userId">The user requesting the lock.</param>
    /// <param name="userEmail">The user's email for display purposes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lock acquisition result with status.</returns>
    Task<EditorLockResult> AcquireLockAsync(Guid gameId, Guid userId, string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a lock held by the specified user.
    /// </summary>
    /// <param name="gameId">The game ID to unlock.</param>
    /// <param name="userId">The user releasing the lock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if lock was released, false if user didn't hold the lock.</returns>
    Task<bool> ReleaseLockAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an existing lock to extend its TTL.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <param name="userId">The user holding the lock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if lock was refreshed, false if user doesn't hold the lock.</returns>
    Task<bool> RefreshLockAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current lock status for a RuleSpec.
    /// </summary>
    /// <param name="gameId">The game ID to check.</param>
    /// <param name="currentUserId">The current user's ID for comparison.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lock status information.</returns>
    Task<EditorLockDto> GetLockStatusAsync(Guid gameId, Guid currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Force releases a lock (admin only).
    /// </summary>
    /// <param name="gameId">The game ID to force unlock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ForceReleaseLockAsync(Guid gameId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a lock acquisition attempt.
/// </summary>
internal record EditorLockResult(
    bool Success,
    EditorLockDto LockStatus,
    string? Message = null
);

/// <summary>
/// Internal lock data stored in Redis.
/// </summary>
internal sealed record EditorLockData(
    Guid GameId,
    Guid UserId,
    string UserEmail,
    DateTime LockedAt,
    DateTime ExpiresAt
);

/// <summary>
/// Issue #2055: Redis-based implementation of editor lock service.
/// Uses distributed cache with TTL for automatic lock expiry.
/// </summary>
internal class EditorLockService : IEditorLockService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<EditorLockService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Lock TTL - locks expire after this duration of inactivity.
    /// </summary>
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Redis key prefix for editor locks.
    /// </summary>
    private const string LockKeyPrefix = "editor:lock:rulespec:";

    public EditorLockService(
        IDistributedCache cache,
        ILogger<EditorLockService> logger,
        TimeProvider timeProvider)
    {
        _cache = cache;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<EditorLockResult> AcquireLockAsync(
        Guid gameId,
        Guid userId,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        var key = GetLockKey(gameId);
        var existingLock = await GetLockDataAsync(key, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Check if lock exists and is still valid
        if (existingLock != null && existingLock.ExpiresAt > now)
        {
            // Lock is held by another user
            if (existingLock.UserId != userId)
            {
                _logger.LogInformation(
                    "Lock acquisition denied for game {GameId}: held by user {ExistingUserId} until {ExpiresAt}",
                    gameId, existingLock.UserId, existingLock.ExpiresAt);

                return new EditorLockResult(
                    Success: false,
                    LockStatus: CreateLockDto(gameId, existingLock, userId),
                    Message: $"RuleSpec is being edited by {MaskEmail(existingLock.UserEmail)}. Try again later or request edit access."
                );
            }

            // User already holds the lock - refresh it
            return await RefreshAndReturnAsync(gameId, existingLock, userId, cancellationToken).ConfigureAwait(false);
        }

        // No valid lock exists - create new lock
        var lockData = new EditorLockData(
            GameId: gameId,
            UserId: userId,
            UserEmail: userEmail,
            LockedAt: now,
            ExpiresAt: now.Add(LockTtl)
        );

        await SetLockDataAsync(key, lockData, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Lock acquired for game {GameId} by user {UserId}, expires at {ExpiresAt}",
            gameId, userId, lockData.ExpiresAt);

        return new EditorLockResult(
            Success: true,
            LockStatus: CreateLockDto(gameId, lockData, userId),
            Message: "Lock acquired successfully"
        );
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseLockAsync(
        Guid gameId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var key = GetLockKey(gameId);
        var existingLock = await GetLockDataAsync(key, cancellationToken).ConfigureAwait(false);

        // No lock exists or lock expired
        if (existingLock == null || existingLock.ExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            _logger.LogDebug("No active lock to release for game {GameId}", gameId);
            return true;
        }

        // Check if user owns the lock
        if (existingLock.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to release lock held by {LockHolder} for game {GameId}",
                userId, existingLock.UserId, gameId);
            return false;
        }

        await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Lock released for game {GameId} by user {UserId}", gameId, userId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RefreshLockAsync(
        Guid gameId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var key = GetLockKey(gameId);
        var existingLock = await GetLockDataAsync(key, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // No lock or expired lock
        if (existingLock == null || existingLock.ExpiresAt <= now)
        {
            _logger.LogDebug("No active lock to refresh for game {GameId}", gameId);
            return false;
        }

        // Check if user owns the lock
        if (existingLock.UserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to refresh lock held by {LockHolder} for game {GameId}",
                userId, existingLock.UserId, gameId);
            return false;
        }

        // Refresh the lock with new expiry
        var refreshedLock = existingLock with { ExpiresAt = now.Add(LockTtl) };
        await SetLockDataAsync(key, refreshedLock, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Lock refreshed for game {GameId} by user {UserId}, new expiry: {ExpiresAt}",
            gameId, userId, refreshedLock.ExpiresAt);

        return true;
    }

    /// <inheritdoc />
    public async Task<EditorLockDto> GetLockStatusAsync(
        Guid gameId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var key = GetLockKey(gameId);
        var existingLock = await GetLockDataAsync(key, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // No lock or expired lock
        if (existingLock == null || existingLock.ExpiresAt <= now)
        {
            return new EditorLockDto(
                GameId: gameId,
                LockedByUserId: null,
                LockedByUserEmail: null,
                LockedAt: null,
                ExpiresAt: null,
                IsLocked: false,
                IsCurrentUserLock: false
            );
        }

        return CreateLockDto(gameId, existingLock, currentUserId);
    }

    /// <inheritdoc />
    public async Task ForceReleaseLockAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var key = GetLockKey(gameId);
        await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("Lock force-released for game {GameId}", gameId);
    }

    private static string GetLockKey(Guid gameId) => $"{LockKeyPrefix}{gameId}";

    private async Task<EditorLockData?> GetLockDataAsync(string key, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<EditorLockData>(json);
    }

    private async Task SetLockDataAsync(string key, EditorLockData data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = data.ExpiresAt
        };

        await _cache.SetStringAsync(key, json, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<EditorLockResult> RefreshAndReturnAsync(
        Guid gameId,
        EditorLockData existingLock,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var refreshedLock = existingLock with { ExpiresAt = now.Add(LockTtl) };
        await SetLockDataAsync(GetLockKey(gameId), refreshedLock, cancellationToken).ConfigureAwait(false);

        return new EditorLockResult(
            Success: true,
            LockStatus: CreateLockDto(gameId, refreshedLock, userId),
            Message: "Lock refreshed"
        );
    }

    private static EditorLockDto CreateLockDto(Guid gameId, EditorLockData data, Guid currentUserId)
    {
        return new EditorLockDto(
            GameId: gameId,
            LockedByUserId: data.UserId,
            LockedByUserEmail: MaskEmail(data.UserEmail),
            LockedAt: data.LockedAt,
            ExpiresAt: data.ExpiresAt,
            IsLocked: true,
            IsCurrentUserLock: data.UserId == currentUserId
        );
    }

    /// <summary>
    /// Masks email for privacy: john.doe@example.com -> j***@e***.com
    /// </summary>
    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "Unknown user";
        }

        var parts = email.Split('@');
        if (parts.Length != 2)
        {
            return "***";
        }

        var localPart = parts[0];
        var domainParts = parts[1].Split('.');

        var maskedLocal = localPart.Length > 0
            ? $"{localPart[0]}***"
            : "***";

        var maskedDomain = domainParts.Length > 0
            ? $"{domainParts[0][0]}***.{domainParts[^1]}"
            : "***";

        return $"{maskedLocal}@{maskedDomain}";
    }
}
