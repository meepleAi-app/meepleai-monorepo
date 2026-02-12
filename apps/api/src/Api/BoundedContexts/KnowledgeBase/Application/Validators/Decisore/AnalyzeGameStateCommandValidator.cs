using Api.BoundedContexts.KnowledgeBase.Application.Commands.Decisore;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.Decisore;

/// <summary>
/// Validator for AnalyzeGameStateCommand.
/// Issue #3769: Validates Decisore analysis request parameters.
/// </summary>
internal sealed class AnalyzeGameStateCommandValidator : AbstractValidator<AnalyzeGameStateCommand>
{
    private static readonly string[] s_allowedDepths = { "quick", "standard", "deep" };

    public AnalyzeGameStateCommandValidator()
    {
        RuleFor(x => x.GameSessionId)
            .NotEmpty().WithMessage("GameSessionId is required");

        RuleFor(x => x.PlayerName)
            .NotEmpty().WithMessage("PlayerName is required")
            .MaximumLength(100).WithMessage("PlayerName must not exceed 100 characters");

        RuleFor(x => x.AnalysisDepth)
            .Must(d => s_allowedDepths.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"AnalysisDepth must be one of: {string.Join(", ", s_allowedDepths)}");

        RuleFor(x => x.MaxSuggestions)
            .InclusiveBetween(1, 10).WithMessage("MaxSuggestions must be between 1 and 10");
    }
}
