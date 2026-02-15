using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for SubmitValidationFeedbackCommand.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
internal sealed class SubmitValidationFeedbackCommandValidator : AbstractValidator<SubmitValidationFeedbackCommand>
{
    private static readonly string[] ValidAccuracyValues = ["Correct", "Incorrect", "Uncertain"];
    private static readonly string[] ValidAiDecisions = ["VALID", "INVALID", "UNCERTAIN"];

    public SubmitValidationFeedbackCommandValidator()
    {
        RuleFor(x => x.ValidationId)
            .NotEmpty().WithMessage("ValidationId is required");

        RuleFor(x => x.GameSessionId)
            .NotEmpty().WithMessage("GameSessionId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5");

        RuleFor(x => x.Accuracy)
            .NotEmpty().WithMessage("Accuracy is required")
            .Must(a => ValidAccuracyValues.Contains(a, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Accuracy must be one of: Correct, Incorrect, Uncertain");

        RuleFor(x => x.Comment)
            .MaximumLength(2000).WithMessage("Comment must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Comment));

        RuleFor(x => x.AiDecision)
            .NotEmpty().WithMessage("AiDecision is required")
            .Must(d => ValidAiDecisions.Contains(d.ToUpperInvariant(), StringComparer.Ordinal))
            .WithMessage("AiDecision must be one of: VALID, INVALID, UNCERTAIN");

        RuleFor(x => x.AiConfidence)
            .InclusiveBetween(0.0, 1.0).WithMessage("AiConfidence must be between 0.0 and 1.0");
    }
}
