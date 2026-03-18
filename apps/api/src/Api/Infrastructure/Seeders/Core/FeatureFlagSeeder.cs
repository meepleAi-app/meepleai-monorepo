using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Core;

/// <summary>
/// Seeds default feature flag configurations with tier-based access.
/// Idempotent - can be run multiple times safely.
/// Issue #3674: Feature Flag Tier-Based Access Verification
/// </summary>
internal static class FeatureFlagSeeder
{
    /// <summary>
    /// Default feature flag definitions with their tier access settings.
    /// Each entry defines: featureName, description, global enabled, free tier, normal tier, premium tier
    /// </summary>
    private static readonly FeatureFlagSeedData[] DefaultFeatureFlags =
    [
        new("basic_chat", "Basic AI chat for game rules questions", true, true, true, true),
        new("advanced_rag", "Advanced RAG with hybrid retrieval", true, false, true, true),
        new("multi_agent", "Multi-agent AI conversations", true, false, false, true),
        new("pdf_ocr", "PDF upload and OCR processing", true, false, true, true),
        new("chat_export", "Export chat conversations", true, true, true, true),
        new("custom_collections", "Custom game collections", true, true, true, true),
        new("advanced_search", "Advanced search with filters", true, false, true, true),
        new("game_recommendations", "AI-powered game recommendations", true, false, true, true),
        new("bulk_import", "Bulk game library import", true, false, false, true),
        new("api_access", "REST API access via API keys", true, false, false, true),
        new("Features.PdfUpload", "Enable/disable PDF upload feature (standard + chunked)", true, true, true, true),
        new("Features.StreamingResponses", "Enable/disable SSE streaming AI responses", true, true, true, true),
        new("alpha-layout", "Alpha mobile-first layout with 4-tab navigation (AlphaShell)", true, true, true, true),
    ];

    /// <summary>
    /// Seeds default feature flag configurations for all tiers.
    /// </summary>
    public static async Task SeedFeatureFlagsAsync(
        MeepleAiDbContext db,
        Guid adminUserId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting FeatureFlag seed for default tier configurations");

        var seededCount = 0;
        var skippedCount = 0;

        foreach (var flag in DefaultFeatureFlags)
        {
            // Seed global flag
            var globalSeeded = await SeedConfigEntryAsync(
                db, flag.FeatureName, flag.GlobalEnabled.ToString().ToLowerInvariant(),
                flag.Description, adminUserId, cancellationToken).ConfigureAwait(false);

            if (globalSeeded) seededCount++; else skippedCount++;

            // Seed tier-specific flags
            var tierFlags = new[]
            {
                (Tier: "free", Enabled: flag.FreeEnabled),
                (Tier: "normal", Enabled: flag.NormalEnabled),
                (Tier: "premium", Enabled: flag.PremiumEnabled),
            };

            foreach (var (tier, enabled) in tierFlags)
            {
                var tierKey = $"{flag.FeatureName}.Tier.{tier}";
                var tierDesc = $"Feature flag: {flag.FeatureName} (tier: {tier})";
                var tierSeeded = await SeedConfigEntryAsync(
                    db, tierKey, enabled.ToString().ToLowerInvariant(),
                    tierDesc, adminUserId, cancellationToken).ConfigureAwait(false);

                if (tierSeeded) seededCount++; else skippedCount++;
            }
        }

        logger.LogInformation(
            "FeatureFlag seed completed: {Seeded} created, {Skipped} already existed",
            seededCount, skippedCount);
    }

    private static async Task<bool> SeedConfigEntryAsync(
        MeepleAiDbContext db,
        string key,
        string value,
        string description,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Set<SystemConfigurationEntity>()
            .AnyAsync(c => c.Key == key && c.Category == "FeatureFlags", cancellationToken)
            .ConfigureAwait(false);

        if (exists)
            return false;

        db.Set<SystemConfigurationEntity>().Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = key,
            Value = value,
            ValueType = "Boolean",
            Description = description,
            Category = "FeatureFlags",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUserId,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private sealed record FeatureFlagSeedData(
        string FeatureName,
        string Description,
        bool GlobalEnabled,
        bool FreeEnabled,
        bool NormalEnabled,
        bool PremiumEnabled);
}
