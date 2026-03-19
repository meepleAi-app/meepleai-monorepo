using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for SubmitValidationFeedbackCommand - collects user feedback on Arbitro validations.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
internal sealed class SubmitValidationFeedbackCommandHandler : IRequestHandler<SubmitValidationFeedbackCommand, Guid>
{
    private readonly IArbitroValidationFeedbackRepository _feedbackRepository;
    private readonly IGameSessionRepository _gameSessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubmitValidationFeedbackCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public SubmitValidationFeedbackCommandHandler(
        IArbitroValidationFeedbackRepository feedbackRepository,
        IGameSessionRepository gameSessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<SubmitValidationFeedbackCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _feedbackRepository = feedbackRepository ?? throw new ArgumentNullException(nameof(feedbackRepository));
        _gameSessionRepository = gameSessionRepository ?? throw new ArgumentNullException(nameof(gameSessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Guid> Handle(
        SubmitValidationFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Handling SubmitValidationFeedbackCommand: validation={ValidationId}, user={UserId}, rating={Rating}, accuracy={Accuracy}",
            request.ValidationId,
            request.UserId,
            request.Rating,
            request.Accuracy);

        // Verify GameSession exists
        var session = await _gameSessionRepository
            .GetByIdAsync(request.GameSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            throw new NotFoundException("GameSession", request.GameSessionId.ToString());
        }

        // Check if feedback already exists for this validation
        var existingFeedback = await _feedbackRepository
            .GetByValidationIdAsync(request.ValidationId, cancellationToken)
            .ConfigureAwait(false);

        if (existingFeedback != null)
        {
            throw new ConflictException($"Feedback already submitted for validation {request.ValidationId}");
        }

        // Parse accuracy enum
        if (!Enum.TryParse<AccuracyAssessment>(request.Accuracy, ignoreCase: true, out var accuracy))
        {
            throw new InvalidOperationException($"Invalid accuracy value: {request.Accuracy}");
        }

        // Create feedback entity
        var feedback = ArbitroValidationFeedback.Create(
            validationId: request.ValidationId,
            gameSessionId: request.GameSessionId,
            userId: request.UserId,
            rating: request.Rating,
            accuracy: accuracy,
            aiDecision: request.AiDecision,
            aiConfidence: request.AiConfidence,
            hadConflicts: request.HadConflicts,
            comment: request.Comment,
            timeProvider: _timeProvider);

        // Save feedback
        await _feedbackRepository.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "SubmitValidationFeedbackCommand completed: feedbackId={FeedbackId}, validation={ValidationId}",
            feedback.Id,
            request.ValidationId);

        return feedback.Id;
    }
}
