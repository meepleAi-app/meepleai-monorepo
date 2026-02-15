using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GenerateFaqFromValidationCommand - auto-generates FAQ from validated conflicts.
/// Issue #4328: Arbitro Agent Beta Testing - FAQ Auto-Expansion.
/// </summary>
internal sealed class GenerateFaqFromValidationCommandHandler : IRequestHandler<GenerateFaqFromValidationCommand, Guid?>
{
    private readonly IArbitroValidationFeedbackRepository _feedbackRepository;
    private readonly IRuleConflictFaqRepository _faqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateFaqFromValidationCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    // Thresholds for auto-generation
    private const double MinConfidenceForFaq = 0.85; // High confidence required
    // private const int MinUsageCount = 3; // Future: Pattern occurrence threshold for batch generation

    public GenerateFaqFromValidationCommandHandler(
        IArbitroValidationFeedbackRepository feedbackRepository,
        IRuleConflictFaqRepository faqRepository,
        IUnitOfWork unitOfWork,
        ILogger<GenerateFaqFromValidationCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _feedbackRepository = feedbackRepository ?? throw new ArgumentNullException(nameof(feedbackRepository));
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Guid?> Handle(
        GenerateFaqFromValidationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Generating FAQ from validation: validationId={ValidationId}, autoApprove={AutoApprove}",
            request.ValidationId,
            request.AutoApprove);

        // 1. Get feedback for this validation
        var feedback = await _feedbackRepository
            .GetByValidationIdAsync(request.ValidationId, cancellationToken)
            .ConfigureAwait(false);

        if (feedback == null)
        {
            _logger.LogWarning("No feedback found for validation {ValidationId}", request.ValidationId);
            return null;
        }

        // 2. Validate eligibility for FAQ generation
        if (!IsEligibleForFaqGeneration(feedback))
        {
            _logger.LogInformation(
                "Validation not eligible for FAQ: validationId={ValidationId}, accuracy={Accuracy}, confidence={Confidence:F2}, hadConflicts={HadConflicts}",
                request.ValidationId,
                feedback.Accuracy,
                feedback.AiConfidence,
                feedback.HadConflicts);
            return null;
        }

        // 3. Extract pattern from conflict (requires conflict data - placeholder for now)
        // In real implementation, this would analyze the conflict pattern from validation logs
        var pattern = GeneratePatternFromFeedback(feedback);

        // 4. Check if FAQ already exists for this pattern
        var existingFaq = await _faqRepository.FindByPatternAsync(
            feedback.GameSessionId, // Using session ID as proxy for GameId
            pattern,
            cancellationToken).ConfigureAwait(false);

        if (existingFaq != null)
        {
            _logger.LogInformation("FAQ already exists for pattern '{Pattern}'", pattern);
            return existingFaq.Id;
        }

        // 5. Create new FAQ entry
        var faq = RuleConflictFAQ.Create(
            id: Guid.NewGuid(),
            gameId: feedback.GameSessionId, // Placeholder - should be actual GameId from session
            conflictType: ConflictType.Contradiction, // Inferred from pattern
            pattern: pattern,
            resolution: feedback.Comment ?? $"AI decision: {feedback.AiDecision}",
            priority: CalculatePriority(feedback),
            timeProvider: _timeProvider);

        await _faqRepository.AddAsync(faq, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "FAQ auto-generated: faqId={FaqId}, pattern={Pattern}, fromValidation={ValidationId}, autoApproved={AutoApprove}",
            faq.Id,
            pattern,
            request.ValidationId,
            request.AutoApprove);

        return faq.Id;
    }

    private static bool IsEligibleForFaqGeneration(Domain.Entities.ArbitroValidationFeedback feedback)
    {
        return feedback.HadConflicts &&
               feedback.Accuracy == Domain.Entities.AccuracyAssessment.Correct &&
               feedback.AiConfidence >= MinConfidenceForFaq &&
               feedback.Rating >= 4; // High user satisfaction
    }

    private static string GeneratePatternFromFeedback(Domain.Entities.ArbitroValidationFeedback feedback)
    {
        // Simplified pattern generation - in real impl, would analyze conflict details
        // For now, use AI decision + confidence range as pattern
        return $"{feedback.AiDecision.ToLowerInvariant()}_high_confidence";
    }

    private static int CalculatePriority(Domain.Entities.ArbitroValidationFeedback feedback)
    {
        // Higher confidence + higher rating = higher priority
        var confidenceScore = (int)(feedback.AiConfidence * 5); // 0-5
        var ratingScore = feedback.Rating; // 1-5
        var totalScore = (confidenceScore + ratingScore) / 2.0;

        return (int)Math.Ceiling(totalScore); // 1-10 scale
    }
}
