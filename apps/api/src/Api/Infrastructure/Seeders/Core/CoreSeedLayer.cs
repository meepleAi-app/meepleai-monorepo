using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Core;

/// <summary>
/// Core seed layer: admin user, AI models, feature flags, rate limits, badges, tier definitions.
/// Runs in all profiles (Prod, Staging, Dev).
/// </summary>
internal sealed class CoreSeedLayer : ISeedLayer
{
    public string Name => "Core";
    public SeedProfile MinimumProfile => SeedProfile.Prod;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        var mediator = context.Services.GetRequiredService<IMediator>();
        var db = context.DbContext;
        var logger = context.Logger;

        // Fatal: admin user must exist
        logger.LogInformation("[Core] Seeding admin user...");
        await mediator.Send(new SeedAdminUserCommand(), cancellationToken).ConfigureAwait(false);

        // Non-fatal: test user (requires SEED_TEST_PASSWORD secret)
        await SafeExecute("test user",
            () => mediator.Send(new SeedTestUserCommand(), cancellationToken), logger).ConfigureAwait(false);

        // Non-fatal: staging demo user (only runs in Staging environment)
        await SafeExecute("staging demo user",
            () => mediator.Send(new SeedStagingDemoUserCommand(), cancellationToken), logger).ConfigureAwait(false);

        // Non-fatal: log + continue on failure
        await SafeExecute("AI models",
            () => mediator.Send(new SeedAiModelsCommand(), cancellationToken), logger).ConfigureAwait(false);

        // Resolve admin user ID for FK constraints
        var adminUserId = await GetAdminUserIdAsync(db, cancellationToken).ConfigureAwait(false);

        await SafeExecute("feature flags",
            () => FeatureFlagSeeder.SeedFeatureFlagsAsync(db, adminUserId, logger, cancellationToken), logger)
            .ConfigureAwait(false);
        await SafeExecute("rate limit configs",
            () => RateLimitConfigSeeder.SeedRateLimitConfigsAsync(db, logger, cancellationToken), logger)
            .ConfigureAwait(false);
        await SafeExecute("badges",
            () => BadgeSeeder.SeedBadgesAsync(db, logger, cancellationToken), logger)
            .ConfigureAwait(false);
        await SafeExecute("tier definitions",
            () => TierDefinitionSeeder.SeedTierDefinitionsAsync(db, logger, cancellationToken), logger)
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
            logger.LogWarning(ex, "[Core] Seeder '{Name}' failed — continuing", name);
        }
    }
}
