using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for RemoveDocumentFromCollectionCommand.
/// Validates GUID properties are non-empty.
/// </summary>
internal sealed class RemoveDocumentFromCollectionCommandValidator : AbstractValidator<RemoveDocumentFromCollectionCommand>
{
    public RemoveDocumentFromCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty()
            .WithMessage("Collection ID is required.");

        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PDF document ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");
    }
}
