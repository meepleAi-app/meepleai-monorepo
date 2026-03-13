using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds default tier definitions (free + premium) into the database.
/// Idempotent - can be run multiple times safely.
/// D3: Game Night Flow - tier system definitions.
/// </summary>
internal static class TierDefinitionSeeder
{
    private static readonly Guid FreeTierId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PremiumTierId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Seeds free and premium tier definitions.
    /// </summary>
    public static async Task SeedTierDefinitionsAsync(
        MeepleAiDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting TierDefinition seed for free and premium tiers");

        var seededCount = 0;
        var skippedCount = 0;

        // Seed free tier
        var freeSeeded = await SeedTierAsync(db, FreeTierId, "free", "Free",
            TierLimits.FreeTier, "free", isDefault: true, cancellationToken).ConfigureAwait(false);
        if (freeSeeded) seededCount++; else skippedCount++;

        // Seed premium tier
        var premiumSeeded = await SeedTierAsync(db, PremiumTierId, "premium", "Premium",
            TierLimits.PremiumTier, "standard", isDefault: false, cancellationToken).ConfigureAwait(false);
        if (premiumSeeded) seededCount++; else skippedCount++;

        logger.LogInformation(
            "TierDefinition seed completed: {Seeded} created, {Skipped} already existed",
            seededCount, skippedCount);
    }

    private static async Task<bool> SeedTierAsync(
        MeepleAiDbContext db,
        Guid id,
        string name,
        string displayName,
        TierLimits limits,
        string llmModelTier,
        bool isDefault,
        CancellationToken cancellationToken)
    {
        var exists = await db.Set<TierDefinition>()
            .AnyAsync(t => t.Name == name, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
            return false;

        // Use reflection to set Id since the factory method generates a new one.
        // This ensures deterministic IDs for seed data.
        var tier = TierDefinition.Create(name, displayName, limits, llmModelTier, isDefault);

        // Set the deterministic ID via EF change tracker
        db.Set<TierDefinition>().Add(tier);
        db.Entry(tier).Property(t => t.Id).CurrentValue = id;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
