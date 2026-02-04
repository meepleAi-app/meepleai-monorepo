using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Validator for AskAgentQuestionCommand
/// </summary>
internal class AskAgentQuestionCommandValidator : AbstractValidator<AskAgentQuestionCommand>
{
    private static readonly string[] s_supportedLanguages = { "en", "it", "de", "fr", "es" };

    public AskAgentQuestionCommandValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required")
            .MaximumLength(2000).WithMessage("Question must not exceed 2000 characters");

        RuleFor(x => x.Strategy)
            .IsInEnum().WithMessage("Invalid search strategy");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, 20).WithMessage("TopK must be between 1 and 20");

        RuleFor(x => x.MinScore)
            .InclusiveBetween(0.0, 1.0).WithMessage("MinScore must be between 0 and 1");

        RuleFor(x => x.Language)
            .Must(lang => lang == null || s_supportedLanguages.Contains(lang, StringComparer.Ordinal))
            .WithMessage("Language must be one of: en, it, de, fr, es");
    }
}
