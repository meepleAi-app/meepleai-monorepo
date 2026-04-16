using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;

/// <summary>
/// Returns paginated SharedGames where HasKnowledgeBase = false (no indexed VectorDocument).
/// Supports pagination and full-text search on Title.
/// </summary>
internal sealed record GetGamesWithoutKbQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IQuery<GamesWithoutKbPagedResponse>;
