using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.Commands;

/// <summary>
/// Validator for AssembleContextCommand.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
internal sealed class AssembleContextCommandValidator : AbstractValidator<AssembleContextCommand>
{
    public AssembleContextCommandValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query is required")
            .MaximumLength(5000)
            .WithMessage("Query cannot exceed 5000 characters");

        RuleFor(x => x.MaxTotalTokens)
            .GreaterThan(0)
            .WithMessage("MaxTotalTokens must be positive")
            .LessThanOrEqualTo(128000)
            .WithMessage("MaxTotalTokens cannot exceed 128000 (max model context)");

        RuleFor(x => x.MinRelevance)
            .GreaterThanOrEqualTo(0.0)
            .WithMessage("MinRelevance must be at least 0.0")
            .LessThanOrEqualTo(1.0)
            .WithMessage("MinRelevance cannot exceed 1.0");

        When(x => x.SourcePriorities != null, () =>
        {
            RuleFor(x => x.SourcePriorities)
                .Must(priorities => priorities!.All(kvp => kvp.Value >= 0 && kvp.Value <= 100))
                .WithMessage("All source priorities must be between 0 and 100");
        });

        When(x => x.MinTokensPerSource != null, () =>
        {
            RuleFor(x => x.MinTokensPerSource)
                .Must(tokens => tokens!.All(kvp => kvp.Value >= 0))
                .WithMessage("MinTokensPerSource values must be non-negative");
        });

        When(x => x.MaxTokensPerSource != null, () =>
        {
            RuleFor(x => x.MaxTokensPerSource)
                .Must(tokens => tokens!.All(kvp => kvp.Value >= 0))
                .WithMessage("MaxTokensPerSource values must be non-negative");
        });
    }
}
