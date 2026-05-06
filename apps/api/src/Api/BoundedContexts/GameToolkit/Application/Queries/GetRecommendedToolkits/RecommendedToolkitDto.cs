namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

/// <summary>
/// DTO for a recommended toolkit entry on the Discover dashboard.
/// Stub placeholder — full implementation in Task C (GetRecommendedToolkitsQuery handler).
/// Issue #728.
/// </summary>
internal sealed record RecommendedToolkitDto(
    Guid Id,
    string Name,
    string GameName,
    int UsageCount,
    DateTime CreatedAt
);
