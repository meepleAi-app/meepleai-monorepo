using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Validator for SaveMechanicDraftCommand.
/// </summary>
internal sealed class SaveMechanicDraftCommandValidator : AbstractValidator<SaveMechanicDraftCommand>
{
    public SaveMechanicDraftCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("Shared game ID is required");

        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PDF document ID is required");

        RuleFor(x => x.GameTitle)
            .NotEmpty()
            .WithMessage("Game title is required")
            .MaximumLength(300)
            .WithMessage("Game title cannot exceed 300 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
