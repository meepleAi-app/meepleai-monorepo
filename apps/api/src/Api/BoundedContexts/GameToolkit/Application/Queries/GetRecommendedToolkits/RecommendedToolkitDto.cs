namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

/// <summary>
/// Lightweight DTO for the SP4 /discover route's "Recommended toolkits" rail
/// (Wave 3 Phase 4a, PR #732 §4.3.4 / Issue #805).
/// </summary>
/// <remarks>
/// Powers the FE <c>useDiscoverRecommendedToolkits</c> hook (Wave 3 Step 3b).
///
/// <para>
/// Schema reality v1 carryover (Gate B) — flagged in field comments:
/// <list type="bullet">
///   <item><see cref="InstallCount"/>: no <c>ToolkitInstallation</c> entity yet → always 0.</item>
///   <item><see cref="RatingAverage"/>: no <c>ToolkitRating</c> entity yet → always null.</item>
///   <item><see cref="RatingCount"/>: same as <see cref="RatingAverage"/> → always 0.</item>
///   <item><see cref="CoverImageUrl"/>: no cover image column on
///     <c>GameToolkitEntity</c> in v1 → always null.</item>
/// </list>
/// </para>
///
/// <para>
/// Recommendation algorithm (per Nygard §4.3.4 spec):
/// <c>(ratingAverage * log(ratingCount + 1)) DESC, installCount DESC tiebreak,
/// createdAt DESC final tiebreak</c>. With all P22 stubs at 0/null, the effective
/// sort collapses to <c>createdAt DESC</c> in v1.
/// </para>
/// </remarks>
internal sealed record RecommendedToolkitDto(
    Guid Id,
    string Name,
    string AuthorName,
    int InstallCount,
    decimal? RatingAverage,
    int RatingCount,
    string? CoverImageUrl
);

/// <summary>
/// Stable response envelope for the recommended-toolkits endpoint.
/// </summary>
internal sealed record RecommendedToolkitsResponse(
    IReadOnlyList<RecommendedToolkitDto> Items
);
