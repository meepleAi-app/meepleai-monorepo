using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds default rate limit configurations for different user tiers.
/// Idempotent - can be run multiple times safely.
/// ISSUE-2809: Infrastructure - SystemConfiguration Rate Limit Entities
/// </summary>
internal static class RateLimitConfigSeeder
{
    /// <summary>
    /// Seeds default rate limit configurations for Free, Premium, and Pro tiers.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">Logger for tracking seed progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SeedRateLimitConfigsAsync(
        MeepleAiDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🌱 Starting RateLimitConfig seed for default tier configurations");

        var seededCount = 0;
        var skippedCount = 0;

        // Define default configurations from issue spec:
        // Free: 2 pending, 5/month, 7 days cooldown
        // Premium: 5 pending, 15/month, 3 days cooldown
        // Pro: 10 pending, 30/month, 1 day cooldown
        var defaultConfigs = new[]
        {
            ShareRequestLimitConfig.Create(
                UserTier.Free,
                maxPendingRequests: 2,
                maxRequestsPerMonth: 5,
                cooldownAfterRejection: TimeSpan.FromDays(7)),

            ShareRequestLimitConfig.Create(
                UserTier.Premium,
                maxPendingRequests: 5,
                maxRequestsPerMonth: 15,
                cooldownAfterRejection: TimeSpan.FromDays(3)),

            ShareRequestLimitConfig.Create(
                UserTier.Pro,
                maxPendingRequests: 10,
                maxRequestsPerMonth: 30,
                cooldownAfterRejection: TimeSpan.FromDays(1))
        };

        foreach (var config in defaultConfigs)
        {
            try
            {
                // Check if configuration for this tier already exists
                var exists = await db.ShareRequestLimitConfigs
                    .AnyAsync(c => c.Tier == (int)config.Tier, cancellationToken)
                    .ConfigureAwait(false);

                if (exists)
                {
                    logger.LogDebug("⏭️  RateLimitConfig for tier '{Tier}' already exists, skipping", config.Tier);
                    skippedCount++;
                    continue;
                }

                // Map domain entity to persistence entity
                var entity = new ShareRequestLimitConfigEntity
                {
                    Id = config.Id,
                    Tier = (int)config.Tier,
                    MaxPendingRequests = config.MaxPendingRequests,
                    MaxRequestsPerMonth = config.MaxRequestsPerMonth,
                    CooldownAfterRejectionSeconds = (long)config.CooldownAfterRejection.TotalSeconds,
                    IsActive = config.IsActive,
                    CreatedAt = config.CreatedAt,
                    UpdatedAt = config.UpdatedAt
                };

                db.ShareRequestLimitConfigs.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                seededCount++;
                logger.LogInformation(
                    "⚙️  Seeded RateLimitConfig: {Tier} - {MaxPending} pending, {MaxMonth}/month, {Cooldown} cooldown",
                    config.Tier,
                    config.MaxPendingRequests,
                    config.MaxRequestsPerMonth,
                    config.CooldownAfterRejection);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "⚠️  Failed to seed RateLimitConfig for tier '{Tier}', continuing with others", config.Tier);
                skippedCount++;
            }
        }

        logger.LogInformation("🌱 RateLimitConfig seed completed: {Seeded} seeded, {Skipped} skipped",
            seededCount, skippedCount);
    }
}
