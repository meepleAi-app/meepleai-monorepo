namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// Lightweight DTO for the /api/v1/catalog/games/new endpoint
/// (Wave 3 Phase 1, PR #732 §4.3.2 / Issue #805).
/// </summary>
/// <remarks>
/// Powers the SP4 /discover route's "New games" rail (frontend
/// <c>useDiscoverNewGames</c>). Sort: <c>CreatedAt DESC</c>.
/// Filtered by <c>IsDeleted == false</c>. Cached 1h via HybridCache.
/// </remarks>
internal sealed record NewGameDto(
    Guid Id,
    string Name,
    string? Publisher,
    int? Year,
    string? ImageUrl,
    DateTime CreatedAt
);
