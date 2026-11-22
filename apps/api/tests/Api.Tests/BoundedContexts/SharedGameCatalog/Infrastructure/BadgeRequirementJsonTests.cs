using Api.BoundedContexts.SharedGameCatalog.Domain.Constants;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// #5114: the JSON of <c>badges.requirement</c>. The rows already in the databases were written by the old BadgeSeeder
/// in camelCase and come back from Postgres with jsonb's key order; they must read as their requirement, or every
/// badge evaluates as ContributionCount with no threshold and BadgeEvaluator throws.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BadgeRequirementJsonTests
{
    // Copied from badges.requirement in a seeded database (meepleai_verify, 2026-11-22).
    private const string FirstContributionRow =
        """{"type": 4, "customRule": null, "minDocuments": null, "minApprovalRate": null, "minContributions": null, "topContributorRank": null, "consecutiveApprovalsWithoutChanges": null}""";
    private const string Contributor5Row =
        """{"type": 0, "customRule": null, "minDocuments": null, "minApprovalRate": null, "minContributions": 5, "topContributorRank": null, "consecutiveApprovalsWithoutChanges": null}""";
    private const string QualityContributorRow =
        """{"type": 2, "customRule": null, "minDocuments": null, "minApprovalRate": null, "minContributions": null, "topContributorRank": null, "consecutiveApprovalsWithoutChanges": 5}""";
    private const string DocumentContributorRow =
        """{"type": 1, "customRule": null, "minDocuments": 10, "minApprovalRate": null, "minContributions": null, "topContributorRank": null, "consecutiveApprovalsWithoutChanges": null}""";
    private const string TopContributorRow =
        """{"type": 3, "customRule": null, "minDocuments": null, "minApprovalRate": null, "minContributions": null, "topContributorRank": 10, "consecutiveApprovalsWithoutChanges": null}""";

    public static TheoryData<string, BadgeRequirement> SeededRows => new()
    {
        { FirstContributionRow, BadgeRequirement.ForFirstContribution() },
        { Contributor5Row, BadgeRequirement.ForContributionCount(5) },
        { QualityContributorRow, BadgeRequirement.ForQualityStreak(5) },
        { DocumentContributorRow, BadgeRequirement.ForDocumentCount(10) },
        { TopContributorRow, BadgeRequirement.ForTopContributor(10) },
    };

    [Theory]
    [MemberData(nameof(SeededRows))]
    public void ARowSeededBeforeTheFix_ReadsAsItsRequirement(string row, BadgeRequirement expected)
    {
        BadgeRequirementJson.Deserialize(row, Guid.NewGuid()).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EveryPredefinedRequirement_SurvivesTheRoundTrip()
    {
        foreach (var badge in PredefinedBadges.All)
        {
            BadgeRequirementJson.Deserialize(BadgeRequirementJson.Serialize(badge.Requirement), badge.Id)
                .Should().BeEquivalentTo(badge.Requirement, because: badge.Code);
        }
    }

    [Fact]
    public void ARequirementIsWrittenInTheShapeOfTheSeededRows()
    {
        // The rows already seeded are camelCase: a new row in the same shape keeps the column one format.
        BadgeRequirementJson.Serialize(BadgeRequirement.ForContributionCount(5))
            .Should().Contain("\"type\":0").And.Contain("\"minContributions\":5");
    }
}
