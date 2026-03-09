using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Validator for AcceptMechanicDraftCommand.
/// </summary>
internal sealed class AcceptMechanicDraftCommandValidator : AbstractValidator<AcceptMechanicDraftCommand>
{
    private static readonly HashSet<string> ValidSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "mechanics", "victory", "resources", "phases", "questions"
    };

    public AcceptMechanicDraftCommandValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEmpty()
            .WithMessage("Draft ID is required");

        RuleFor(x => x.Section)
            .NotEmpty()
            .WithMessage("Section is required")
            .Must(s => ValidSections.Contains(s))
            .WithMessage("Section must be one of: summary, mechanics, victory, resources, phases, questions");

        RuleFor(x => x.AcceptedDraft)
            .NotEmpty()
            .WithMessage("Accepted draft content is required");
    }
}
