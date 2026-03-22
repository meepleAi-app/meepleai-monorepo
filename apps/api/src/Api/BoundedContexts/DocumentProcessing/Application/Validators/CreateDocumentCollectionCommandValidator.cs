using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for CreateDocumentCollectionCommand.
/// Ensures GameId and UserId are non-empty, Name is valid, and Description length is bounded.
/// </summary>
internal sealed class CreateDocumentCollectionCommandValidator : AbstractValidator<CreateDocumentCollectionCommand>
{
    public CreateDocumentCollectionCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Name is required (max 200 chars).");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description != null)
            .WithMessage("Description cannot exceed 500 characters.");
    }
}
