using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameReviews;

/// <summary>
/// Command to create a new game review.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal record CreateGameReviewCommand(
    Guid GameId,
    Guid UserId,
    string AuthorName,
    int Rating,
    string Content
) : ICommand<GameReviewDto>;
