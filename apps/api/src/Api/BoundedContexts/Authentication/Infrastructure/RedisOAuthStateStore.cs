using System;
using System.Threading.Tasks;
using Api.BoundedContexts.Authentication.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.Authentication.Infrastructure;

/// <summary>
/// Redis-backed OAuth state storage for distributed deployments.
/// Uses Redis TTL for automatic expiration and Lua scripts for atomic operations.
/// </summary>
public class RedisOAuthStateStore : IOAuthStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisOAuthStateStore> _logger;
    private const string StateKeyPrefix = "meepleai:oauth:state:";

    public RedisOAuthStateStore(
        IConnectionMultiplexer redis,
        ILogger<RedisOAuthStateStore> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StoreStateAsync(string state, TimeSpan expiration)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be null or empty", nameof(state));
        if (expiration <= TimeSpan.Zero)
            throw new ArgumentException("Expiration must be positive", nameof(expiration));

        try
        {
            var db = _redis.GetDatabase();
            var key = StateKeyPrefix + state;

            // Store state with TTL (automatic expiration)
            var success = await db.StringSetAsync(key, DateTime.UtcNow.ToString("O"), expiration);

            if (success)
            {
                _logger.LogDebug("Stored OAuth state {StatePrefix}*** with {Expiration}s TTL",
                    state[..Math.Min(8, state.Length)], expiration.TotalSeconds);
            }
            else
            {
                _logger.LogWarning("Failed to store OAuth state in Redis");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing OAuth state in Redis");
            throw;
        }
    }

    public async Task<bool> ValidateAndRemoveStateAsync(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return false;

        try
        {
            var db = _redis.GetDatabase();
            var key = StateKeyPrefix + state;

            // Atomically check existence and delete (single-use token)
            var deleted = await db.KeyDeleteAsync(key);

            if (deleted)
            {
                _logger.LogDebug("Validated and removed OAuth state {StatePrefix}***",
                    state[..Math.Min(8, state.Length)]);
                return true;
            }

            _logger.LogWarning("OAuth state validation failed: state not found or expired {StatePrefix}***",
                state[..Math.Min(8, state.Length)]);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating OAuth state in Redis");
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return false;

        try
        {
            var db = _redis.GetDatabase();
            var key = StateKeyPrefix + state;

            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking OAuth state existence in Redis");
            return false;
        }
    }

    public async Task<int> CleanupExpiredStatesAsync()
    {
        // Redis automatically removes expired keys via TTL
        // This method is a no-op for Redis implementation
        // but kept for interface compatibility with other implementations
        _logger.LogDebug("CleanupExpiredStatesAsync called (no-op for Redis - TTL handles cleanup)");
        await Task.CompletedTask;
        return 0;
    }
}
