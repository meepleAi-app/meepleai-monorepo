namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitRatings;

/// <summary>
/// 5-star distribution for the ratings breakdown widget
/// (Wave 3 Phase 4b, PR #732 §5.3.3 / Issue #805).
/// </summary>
/// <remarks>
/// Schema reality v1 carryover (Gate B): the <c>ToolkitRating</c> entity does
/// not exist yet — all counts are <c>0</c> until Phase 5 ships persistence.
/// Wire shape is stable so the FE breakdown bars render today and switch to
/// real counts without a refetch shape change.
/// </remarks>
internal sealed record ToolkitRatingsBreakdownDto(
    int Star1,
    int Star2,
    int Star3,
    int Star4,
    int Star5
);

/// <summary>
/// Single rating row.
/// </summary>
internal sealed record ToolkitRatingDto(
    Guid Id,
    string RaterDisplayName,
    string? RaterAvatarUrl,
    int Stars,
    string? Comment,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Cursor-paginated ratings envelope.
/// Per PR #732 §3.4 empty-state contract: empty list is a 200 with
/// <c>{ items: [] }</c>, not 404. <c>NextCursor</c> is <c>null</c> when there
/// are no more pages.
/// </summary>
internal sealed record ToolkitRatingsResponse(
    IReadOnlyList<ToolkitRatingDto> Items,
    string? NextCursor,
    ToolkitRatingsBreakdownDto Breakdown,
    decimal AverageStars,
    int TotalCount
);
