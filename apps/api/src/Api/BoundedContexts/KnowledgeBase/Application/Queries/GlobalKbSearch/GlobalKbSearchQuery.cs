using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GlobalKbSearch;

/// <summary>
/// Query to perform RBAC-filtered hybrid search across all games accessible to the user.
/// Orchestrates: <see cref="Services.IRagAccessService.GetAccessibleGameIdsAsync"/> →
/// <see cref="Services.IMultiGameHybridSearchService.SearchAsync"/> →
/// batch enrichment join (PdfDocument → SharedGame) in one EF query.
/// Issue #1661 (PR-1, Task 4).
/// </summary>
/// <param name="Query">Natural-language search query. Must not be empty.</param>
/// <param name="Limit">Max results per page (default 20, hard cap 50 enforced by validator).</param>
/// <param name="Cursor">
/// Opaque pagination cursor from a previous response's <c>NextCursor</c>.
/// <c>null</c> for the first page.
/// </param>
/// <param name="Mode">Search mode forwarded to <see cref="Services.IMultiGameHybridSearchService"/>.</param>
/// <param name="MinScore">Minimum hybrid score; results below threshold are discarded.</param>
/// <param name="UserId">Authenticated user ID (for RBAC resolution).</param>
/// <param name="Role">Authenticated user role (for RBAC resolution).</param>
/// <param name="DocType">
/// Optional facet (Issue #1686, D-1/D-7): list of document types from the allowlist
/// <c>{ "base", "expansion", "errata", "homerule" }</c> (case-insensitive). When null
/// or empty, no filter is applied (legacy behaviour, D-3). Hard cap of 10 elements.
/// </param>
/// <param name="GameId">
/// Optional facet (Issue #1686, D-5): single SharedGame.Id to narrow results to.
/// When provided AND ∈ accessibleGameIds, the search runs only on that game.
/// When provided AND ∉ accessibleGameIds, returns 200 empty (no info leak).
/// </param>
/// <param name="Language">
/// Optional facet (Issue #1686, D-2): ISO 639-1 code from the allowlist
/// <c>{ "en", "it", "de", "fr", "es" }</c> (case-insensitive). When null, no
/// language filter is applied.
/// </param>
internal sealed record GlobalKbSearchQuery(
    string Query,
    int Limit,
    string? Cursor,
    SearchMode Mode,
    double MinScore,
    Guid UserId,
    UserRole Role,
    IReadOnlyList<string>? DocType = null,
    Guid? GameId = null,
    string? Language = null) : IQuery<GlobalKbSearchResponseDto>;
