using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.SharedKernel.Constants;
using Api.Infrastructure.BackgroundTasks;
using Api.Observability;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Service for cleaning up orphaned analysis tasks in Redis.
/// ISSUE-2528: Background job for automatic cleanup of stale tasks
/// </summary>
internal sealed class OrphanedTaskCleanupService : IOrphanedTaskCleanupService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OrphanedTaskCleanupService> _logger;

    public OrphanedTaskCleanupService(
        IConnectionMultiplexer redis,
        ILogger<OrphanedTaskCleanupService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> CleanupOrphanedTasksAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default)
    {
        var cleanedCount = 0;
        var scannedCount = 0;

        try
        {
            var db = _redis.GetDatabase();
            var pattern = $"{RedisKeyConstants.TaskStatusPrefix}:*";

            _logger.LogInformation(
                "Starting orphaned task cleanup scan: Pattern={Pattern}, Retention={Retention}",
                pattern, retentionPeriod);

            // Use SCAN to iterate over matching keys (cursor-based, non-blocking)
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: 100)
                .ConfigureAwait(false)
                .WithCancellation(cancellationToken))
            {
                scannedCount++;

                try
                {
                    // Get value and TTL
                    var value = await db.StringGetAsync(key).ConfigureAwait(false);
                    if (value.IsNullOrEmpty)
                    {
                        continue;
                    }

                    // Get TTL to calculate age
                    var ttl = await db.KeyTimeToLiveAsync(key).ConfigureAwait(false);

                    // Parse task status and check if it should be cleaned up
                    if (ShouldCleanupTask(value.ToString(), ttl, retentionPeriod, out var status))
                    {
                        // Delete the key
                        var deleted = await db.KeyDeleteAsync(key).ConfigureAwait(false);
                        if (deleted)
                        {
                            cleanedCount++;
                            _logger.LogInformation(
                                "Cleaned up orphaned task: Key={Key}, Status={Status}, Age={Age}",
                                key.ToString(), status, CalculateAge(ttl));
                        }
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
                // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Individual key error isolation
                // Background tasks must handle per-key errors gracefully without terminating the scan.
                // Errors logged for monitoring; individual key failures don't impact overall cleanup.
#pragma warning restore S125
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Error processing key during cleanup scan: Key={Key}",
                        key.ToString());
                }
#pragma warning restore CA1031
            }

            // Track metric for cleaned tasks
            MeepleAiMetrics.OrphanedTasksCleanedTotal.Add(cleanedCount);

            _logger.LogInformation(
                "Orphaned task cleanup completed: Scanned={Scanned}, Cleaned={Cleaned}",
                scannedCount, cleanedCount);

            return cleanedCount;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
        // Errors logged for monitoring; task failures don't impact main application.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Fatal error during orphaned task cleanup scan: Scanned={Scanned}, Cleaned={Cleaned}",
                scannedCount, cleanedCount);

            // Return partial results even on fatal error
            return cleanedCount;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Determines if a task should be cleaned up based on status and age.
    /// </summary>
    private static bool ShouldCleanupTask(
        string value,
        TimeSpan? ttl,
        TimeSpan retentionPeriod,
        out string status)
    {
        status = string.Empty;

        // Try parsing as JSON first (future-proof for structured status)
        try
        {
            var json = JsonDocument.Parse(value);
            if (json.RootElement.TryGetProperty("Status", out var statusElement))
            {
                status = statusElement.GetString() ?? string.Empty;

                // Check if status is Failed or Cancelled
                if (!string.Equals(status, nameof(BackgroundTaskStatus.Failed), StringComparison.Ordinal) &&
                    !string.Equals(status, nameof(BackgroundTaskStatus.Cancelled), StringComparison.Ordinal))
                {
                    return false;
                }

                // Try to get CreatedAt timestamp
                if (json.RootElement.TryGetProperty("CreatedAt", out var createdAtElement) &&
                    createdAtElement.TryGetDateTime(out var createdAt))
                {
                    var age = DateTime.UtcNow - createdAt;
                    return age > retentionPeriod;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON, treat as simple string
        }

        // Fallback: simple string enum value
        status = value;

        // Check if status is Failed or Cancelled
        if (!string.Equals(status, nameof(BackgroundTaskStatus.Failed), StringComparison.Ordinal) &&
            !string.Equals(status, nameof(BackgroundTaskStatus.Cancelled), StringComparison.Ordinal))
        {
            return false;
        }

        // Calculate age from TTL (24h default - remaining TTL)
        if (ttl.HasValue)
        {
            var defaultTtl = TimeSpan.FromHours(24);
            var age = defaultTtl - ttl.Value;
            return age > retentionPeriod;
        }

        // If no TTL, key is persistent - don't delete
        return false;
    }

    /// <summary>
    /// Calculates task age from TTL.
    /// </summary>
    private static TimeSpan CalculateAge(TimeSpan? ttl)
    {
        if (!ttl.HasValue)
        {
            return TimeSpan.Zero;
        }

        var defaultTtl = TimeSpan.FromHours(24);
        return defaultTtl - ttl.Value;
    }
}
