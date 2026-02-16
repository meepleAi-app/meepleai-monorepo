using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for SubmitInsightFeedbackCommand.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
internal sealed class SubmitInsightFeedbackCommandValidator : AbstractValidator<SubmitInsightFeedbackCommand>
{
    private static readonly string[] ValidInsightTypes =
        ["Backlog", "RulesReminder", "Recommendation", "Streak", "Achievement"];

    public SubmitInsightFeedbackCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.InsightId)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.InsightType)
            .NotEmpty()
            .Must(t => ValidInsightTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("InsightType must be: Backlog, RulesReminder, Recommendation, Streak, or Achievement");

        RuleFor(x => x.Comment)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Comment));
    }
}
