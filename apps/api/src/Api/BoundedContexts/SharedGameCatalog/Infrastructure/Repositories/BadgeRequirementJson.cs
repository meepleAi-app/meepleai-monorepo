using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// The one shape of <c>badges.requirement</c>, written by BadgeSeeder and read by BadgeRepository (#5114). They used
/// to disagree: the seeder wrote camelCase, the repository read with the default case-sensitive options, bound no key,
/// and every badge came back as ContributionCount with no threshold, so BadgeEvaluator threw on the first one.
/// </summary>
/// <remarks>
/// Web defaults: camelCase when writing, case-insensitive when reading, so the rows already seeded read as they are.
/// </remarks>
internal static class BadgeRequirementJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(BadgeRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return JsonSerializer.Serialize(
            new Dto(
                (int)requirement.Type,
                requirement.MinContributions,
                requirement.MinDocuments,
                requirement.MinApprovalRate,
                requirement.ConsecutiveApprovalsWithoutChanges,
                requirement.TopContributorRank,
                requirement.CustomRule),
            Options);
    }

    public static BadgeRequirement Deserialize(string json, Guid badgeId)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, Options)
            ?? throw new InvalidOperationException($"Failed to deserialize badge requirement for badge {badgeId}");

        return new BadgeRequirement(
            (BadgeRequirementType)dto.Type,
            dto.MinContributions,
            dto.MinDocuments,
            dto.MinApprovalRate,
            dto.ConsecutiveApprovalsWithoutChanges,
            dto.TopContributorRank,
            dto.CustomRule);
    }

    private sealed record Dto(
        int Type,
        int? MinContributions,
        int? MinDocuments,
        decimal? MinApprovalRate,
        int? ConsecutiveApprovalsWithoutChanges,
        int? TopContributorRank,
        string? CustomRule);
}
