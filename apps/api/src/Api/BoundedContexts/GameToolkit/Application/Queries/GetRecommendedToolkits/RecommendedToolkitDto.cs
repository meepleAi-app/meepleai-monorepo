namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

/// <summary>
/// DTO for a recommended toolkit entry on the Discover dashboard.
/// InstallCount is always 0 — GameToolkitEntity has no install-count field.
/// MVP ranking uses CreatedAt DESC (freshness). Issue #728.
/// </summary>
internal sealed record RecommendedToolkitDto(
    Guid Id,
    string Name,
    string GameName,
    int Version,
    int InstallCount,
    DateTime CreatedAt
);
