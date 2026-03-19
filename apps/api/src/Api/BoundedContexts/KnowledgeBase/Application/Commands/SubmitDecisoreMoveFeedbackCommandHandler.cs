using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for SubmitDecisoreMoveFeedbackCommand.
/// Issue #4335: Decisore Agent Beta Testing and User Feedback Iteration.
/// </summary>
internal sealed class SubmitDecisoreMoveFeedbackCommandHandler : IRequestHandler<SubmitDecisoreMoveFeedbackCommand, Guid>
{
    private readonly IDecisoreMoveFeedbackRepository _feedbackRepository;
    private readonly IGameSessionRepository _gameSessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubmitDecisoreMoveFeedbackCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public SubmitDecisoreMoveFeedbackCommandHandler(
        IDecisoreMoveFeedbackRepository feedbackRepository,
        IGameSessionRepository gameSessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<SubmitDecisoreMoveFeedbackCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _feedbackRepository = feedbackRepository ?? throw new ArgumentNullException(nameof(feedbackRepository));
        _gameSessionRepository = gameSessionRepository ?? throw new ArgumentNullException(nameof(gameSessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Guid> Handle(SubmitDecisoreMoveFeedbackCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "SubmitDecisoreMoveFeedback: suggestion={SuggestionId}, quality={Quality}, followed={Followed}, outcome={Outcome}",
            request.SuggestionId, request.Quality, request.SuggestionFollowed, request.Outcome);

        var session = await _gameSessionRepository.GetByIdAsync(request.GameSessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
            throw new NotFoundException("GameSession", request.GameSessionId.ToString());

        var existingFeedback = await _feedbackRepository.GetBySuggestionIdAsync(request.SuggestionId, cancellationToken).ConfigureAwait(false);
        if (existingFeedback != null)
            throw new ConflictException($"Feedback already submitted for suggestion {request.SuggestionId}");

        if (!Enum.TryParse<MoveQualityAssessment>(request.Quality, ignoreCase: true, out var quality))
            throw new InvalidOperationException($"Invalid quality value: {request.Quality}");

        if (!Enum.TryParse<GameOutcome>(request.Outcome, ignoreCase: true, out var outcome))
            throw new InvalidOperationException($"Invalid outcome value: {request.Outcome}");

        var feedback = DecisoreMoveFeedback.Create(
            suggestionId: request.SuggestionId,
            gameSessionId: request.GameSessionId,
            userId: request.UserId,
            rating: request.Rating,
            quality: quality,
            outcome: outcome,
            suggestionFollowed: request.SuggestionFollowed,
            topSuggestedMove: request.TopSuggestedMove,
            positionStrength: request.PositionStrength,
            analysisDepth: request.AnalysisDepth,
            comment: request.Comment,
            timeProvider: _timeProvider);

        await _feedbackRepository.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("DecisoreMoveFeedback created: feedbackId={FeedbackId}", feedback.Id);

        return feedback.Id;
    }
}
