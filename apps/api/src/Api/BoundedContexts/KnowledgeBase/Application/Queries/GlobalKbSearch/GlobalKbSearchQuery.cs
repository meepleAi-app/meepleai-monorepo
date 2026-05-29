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
internal sealed record GlobalKbSearchQuery(
    string Query,
    int Limit,
    string? Cursor,
    SearchMode Mode,
    double MinScore,
    Guid UserId,
    UserRole Role) : IQuery<GlobalKbSearchResponseDto>;
