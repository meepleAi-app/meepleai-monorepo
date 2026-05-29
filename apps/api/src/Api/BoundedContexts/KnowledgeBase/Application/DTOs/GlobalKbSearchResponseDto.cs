namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Response envelope for cross-game KB search.
/// Mirrors the FE contract §5 shape (kb-globale-hooks.md).
/// Issue #1661: cross-game KB search (Task 4).
/// </summary>
/// <param name="Results">Ranked list of enriched search results (at most <c>Limit</c> items).</param>
/// <param name="HasMore">
/// <c>true</c> when there are additional pages (D6 — avoids expensive cross-game totalCount;
/// detected via <c>Limit + 1</c> probe at the search layer, EC-4).
/// </param>
/// <param name="NextCursor">
/// Opaque Base64 cursor encoding <c>"{lastScore}|{lastChunkId}"</c> for keyset pagination
/// (EC-4). <c>null</c> when <see cref="HasMore"/> is <c>false</c>.
/// </param>
internal sealed record GlobalKbSearchResponseDto(
    IReadOnlyList<GlobalKbSearchResultDto> Results,
    bool HasMore,
    string? NextCursor);
