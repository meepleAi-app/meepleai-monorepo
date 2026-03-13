using Api.Infrastructure.Seeders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Core;

/// <summary>
/// Layer 1: Core seeding. Always runs regardless of profile.
/// Admin user failure is fatal; all others log and continue.
/// </summary>
internal static class CoreSeeder
{
    public static async Task SeedAsync(
        IMediator mediator,
        MeepleAiDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        // Fatal: admin user must exist
        await AdminUserSeeder.SeedAsync(mediator, logger, ct).ConfigureAwait(false);

        // Non-fatal: log + continue on failure
        await SafeExecute("AI models",
            () => AiModelSeeder.SeedAsync(mediator, logger, ct), logger).ConfigureAwait(false);

        // Feature flags need adminUserId for FK
        var adminUserId = await GetAdminUserIdAsync(db, ct).ConfigureAwait(false);

        await SafeExecute("feature flags",
            () => FeatureFlagSeeder.SeedFeatureFlagsAsync(db, adminUserId, logger, ct), logger)
            .ConfigureAwait(false);
        await SafeExecute("rate limit configs",
            () => RateLimitConfigSeeder.SeedRateLimitConfigsAsync(db, logger, ct), logger)
            .ConfigureAwait(false);
        await SafeExecute("badges",
            () => BadgeSeeder.SeedBadgesAsync(db, logger, ct), logger)
            .ConfigureAwait(false);
        await SafeExecute("tier definitions",
            () => TierDefinitionSeeder.SeedTierDefinitionsAsync(db, logger, ct), logger)
            .ConfigureAwait(false);
    }

    private static async Task<Guid> GetAdminUserIdAsync(MeepleAiDbContext db, CancellationToken ct)
    {
        var adminUser = await db.Users
            .FirstOrDefaultAsync(u => u.Role == "admin" || u.Role == "superadmin", ct)
            .ConfigureAwait(false);

        return adminUser?.Id ?? Guid.Empty;
    }

    private static async Task SafeExecute(string name, Func<Task> action, ILogger logger)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Core seeder '{Name}' failed - continuing", name);
        }
    }
}
