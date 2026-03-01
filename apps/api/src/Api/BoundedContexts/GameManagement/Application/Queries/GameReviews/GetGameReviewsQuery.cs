using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameReviews;

/// <summary>
/// Query to get paginated reviews for a game.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal record GetGameReviewsQuery(
    Guid GameId,
    int PageNumber = 1,
    int PageSize = 10
) : IQuery<PagedResult<GameReviewDto>>;
