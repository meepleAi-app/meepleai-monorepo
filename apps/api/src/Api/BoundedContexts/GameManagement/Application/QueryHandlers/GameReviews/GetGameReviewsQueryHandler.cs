using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.GameReviews;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.QueryHandlers.GameReviews;

/// <summary>
/// Handles GetGameReviewsQuery with pagination.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal sealed class GetGameReviewsQueryHandler
    : IQueryHandler<GetGameReviewsQuery, PagedResult<GameReviewDto>>
{
    private readonly IGameReviewRepository _repository;
    private readonly ILogger<GetGameReviewsQueryHandler> _logger;

    public GetGameReviewsQueryHandler(
        IGameReviewRepository repository,
        ILogger<GetGameReviewsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<GameReviewDto>> Handle(
        GetGameReviewsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Retrieving reviews for game {GameId}, page {Page}/{PageSize}",
            query.GameId, query.PageNumber, query.PageSize);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _repository
            .GetBySharedGameIdAsync(query.GameId, pageNumber, pageSize, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} reviews (total: {Total}) for game {GameId}",
            items.Count, totalCount, query.GameId);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<GameReviewDto>(dtos, totalCount, pageNumber, pageSize);
    }

    private static GameReviewDto MapToDto(GameReview review) =>
        new(
            review.Id,
            review.SharedGameId,
            review.AuthorName,
            review.Rating,
            review.Content,
            review.CreatedAt,
            review.UpdatedAt);
}
