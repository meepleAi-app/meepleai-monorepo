namespace Api.BoundedContexts.Administration.Application.Queries.GetTopUserContributors;

/// <summary>
/// Per-source breakdown of contribution counts for a single contributor.
/// Wave 3 Phase 4a, PR #732 §4.3.6 / Issue #805.
/// </summary>
internal sealed record TopUserContributorBreakdownDto(
    int FaqsCount,
    int KbUploadsCount,
    int AgentsCreatedCount
);

/// <summary>
/// Single contributor row for the SP4 /discover "Top contributors" rail
/// (Wave 3 Phase 4a, PR #732 §4.3.6 / Issue #805).
/// </summary>
/// <remarks>
/// <para>
/// Distinct from the existing <see
/// cref="SharedGameCatalog.Application.DTOs.TopContributorDto"/> (sessions/wins
/// score for the public /shared-games sidebar). This surface aggregates
/// <em>contribution sources</em> (FAQs authored, KB documents uploaded,
/// AI agents created) for the discover route.
/// </para>
///
/// <para>
/// Schema reality v1 carryover (Gate B):
/// <list type="bullet">
///   <item><c>AgentsCreatedCount</c> is always <c>0</c> in v1 because
///         <c>AgentDefinition</c> aggregates do not track a
///         <c>CreatedByUserId</c> / <c>OwnerUserId</c> column. The wire shape
///         is stable so the rail can render today and adopt the real metric
///         when ownership tracking lands.</item>
///   <item><c>GameFaqEntity</c> tracks <c>CreatedAt</c> but not a creator FK
///         column in v1 — falls back to <c>0</c> for the same reason.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed record TopUserContributorDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    int ContributionCount,
    TopUserContributorBreakdownDto Breakdown
);

/// <summary>
/// Stable response envelope for the top-contributors endpoint.
/// </summary>
internal sealed record TopUserContributorsResponse(
    IReadOnlyList<TopUserContributorDto> Items
);
