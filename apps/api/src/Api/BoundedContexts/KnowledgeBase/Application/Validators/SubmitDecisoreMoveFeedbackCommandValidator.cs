using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for SubmitDecisoreMoveFeedbackCommand.
/// Issue #4335: Decisore Agent Beta Testing and User Feedback Iteration.
/// </summary>
internal sealed class SubmitDecisoreMoveFeedbackCommandValidator : AbstractValidator<SubmitDecisoreMoveFeedbackCommand>
{
    private static readonly string[] ValidQualityValues = ["Helpful", "Neutral", "Harmful"];
    private static readonly string[] ValidOutcomeValues = ["Win", "Loss", "Draw", "InProgress"];
    private static readonly string[] ValidDepthValues = ["beginner", "intermediate", "expert"];

    public SubmitDecisoreMoveFeedbackCommandValidator()
    {
        RuleFor(x => x.SuggestionId).NotEmpty();
        RuleFor(x => x.GameSessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);

        RuleFor(x => x.Quality)
            .NotEmpty()
            .Must(q => ValidQualityValues.Contains(q, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Quality must be: Helpful, Neutral, or Harmful");

        RuleFor(x => x.Outcome)
            .NotEmpty()
            .Must(o => ValidOutcomeValues.Contains(o, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Outcome must be: Win, Loss, Draw, or InProgress");

        RuleFor(x => x.TopSuggestedMove).NotEmpty().MaximumLength(10);

        RuleFor(x => x.PositionStrength).InclusiveBetween(-1.0, 1.0);

        RuleFor(x => x.AnalysisDepth)
            .NotEmpty()
            .Must(d => ValidDepthValues.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Analysis depth must be: beginner, intermediate, or expert");

        RuleFor(x => x.Comment)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.Comment));
    }
}
