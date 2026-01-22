using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Constants;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds predefined badges into the database.
/// Idempotent - can be run multiple times safely.
/// ISSUE-2731: Infrastructure - EF Core Migrations e Repository
/// </summary>
internal static class BadgeSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Seeds all predefined badges from the PredefinedBadges constant.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">Logger for tracking seed progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SeedBadgesAsync(
        MeepleAiDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🌱 Starting Badge seed from PredefinedBadges");

        var seededCount = 0;
        var skippedCount = 0;

        foreach (var badge in PredefinedBadges.All)
        {
            try
            {
                // Check if badge with this code already exists
                var exists = await db.Badges
                    .AnyAsync(b => b.Code == badge.Code, cancellationToken)
                    .ConfigureAwait(false);

                if (exists)
                {
                    logger.LogDebug("⏭️  Badge '{BadgeCode}' already exists, skipping", badge.Code);
                    skippedCount++;
                    continue;
                }

                // Map domain badge to persistence entity
                var entity = new BadgeEntity
                {
                    Id = badge.Id,
                    Code = badge.Code,
                    Name = badge.Name,
                    Description = badge.Description,
                    IconUrl = badge.IconUrl,
                    Tier = (int)badge.Tier,
                    Category = (int)badge.Category,
                    IsActive = badge.IsActive,
                    DisplayOrder = badge.DisplayOrder,
                    RequirementJson = SerializeBadgeRequirement(badge.Requirement),
                    CreatedAt = badge.CreatedAt,
                    ModifiedAt = badge.ModifiedAt
                };

                db.Badges.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                seededCount++;
                logger.LogInformation("🏅 Seeded Badge: {BadgeCode} - {BadgeName} ({Tier}/{Category})",
                    badge.Code, badge.Name, badge.Tier, badge.Category);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "⚠️  Failed to seed Badge '{BadgeCode}', continuing with others", badge.Code);
                skippedCount++;
            }
        }

        logger.LogInformation("🌱 Badge seed completed: {Seeded} seeded, {Skipped} skipped",
            seededCount, skippedCount);
    }

    private static string SerializeBadgeRequirement(
        BoundedContexts.SharedGameCatalog.Domain.ValueObjects.BadgeRequirement requirement)
    {
        var dto = new BadgeRequirementDto(
            (int)requirement.Type,
            requirement.MinContributions,
            requirement.MinDocuments,
            requirement.MinApprovalRate,
            requirement.ConsecutiveApprovalsWithoutChanges,
            requirement.TopContributorRank,
            requirement.CustomRule);

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// DTO for JSON serialization of BadgeRequirement value object.
    /// </summary>
    private sealed record BadgeRequirementDto(
        int Type,
        int? MinContributions,
        int? MinDocuments,
        decimal? MinApprovalRate,
        int? ConsecutiveApprovalsWithoutChanges,
        int? TopContributorRank,
        string? CustomRule);
}
