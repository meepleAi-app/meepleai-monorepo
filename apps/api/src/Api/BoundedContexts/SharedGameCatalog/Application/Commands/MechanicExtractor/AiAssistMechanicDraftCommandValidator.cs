using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Validator for AiAssistMechanicDraftCommand.
/// </summary>
internal sealed class AiAssistMechanicDraftCommandValidator : AbstractValidator<AiAssistMechanicDraftCommand>
{
    private static readonly HashSet<string> ValidSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "mechanics", "victory", "resources", "phases", "questions"
    };

    public AiAssistMechanicDraftCommandValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEmpty()
            .WithMessage("Draft ID is required");

        RuleFor(x => x.Section)
            .NotEmpty()
            .WithMessage("Section is required")
            .Must(s => ValidSections.Contains(s))
            .WithMessage("Section must be one of: summary, mechanics, victory, resources, phases, questions");

        RuleFor(x => x.HumanNotes)
            .NotEmpty()
            .WithMessage("Human notes are required for AI assistance")
            .MinimumLength(10)
            .WithMessage("Notes must be at least 10 characters for meaningful AI assistance");

        RuleFor(x => x.GameTitle)
            .NotEmpty()
            .WithMessage("Game title is required");
    }
}
