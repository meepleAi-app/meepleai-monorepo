using Api.BoundedContexts.GameManagement.Application.Commands.GameReviews;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameReviews;

/// <summary>
/// Handles game review creation with one-per-user-per-game enforcement.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal sealed class CreateGameReviewCommandHandler
    : ICommandHandler<CreateGameReviewCommand, GameReviewDto>
{
    private readonly IGameReviewRepository _reviewRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateGameReviewCommandHandler(
        IGameReviewRepository reviewRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _reviewRepository = reviewRepository ?? throw new ArgumentNullException(nameof(reviewRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GameReviewDto> Handle(
        CreateGameReviewCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Enforce one review per user per game
        var existing = await _reviewRepository
            .FindByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            throw new ConflictException(
                $"User already has a review for this game");
        }

        var review = GameReview.Create(
            Guid.NewGuid(),
            command.GameId,
            command.UserId,
            command.AuthorName,
            command.Rating,
            command.Content,
            _timeProvider);

        await _reviewRepository.AddAsync(review, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new GameReviewDto(
            review.Id,
            review.SharedGameId,
            review.AuthorName,
            review.Rating,
            review.Content,
            review.CreatedAt,
            review.UpdatedAt);
    }
}
