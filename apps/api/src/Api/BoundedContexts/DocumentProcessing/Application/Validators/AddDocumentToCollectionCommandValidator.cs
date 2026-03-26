using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for AddDocumentToCollectionCommand.
/// Ensures CollectionId, PdfDocumentId, UserId are non-empty, DocumentType is valid, and SortOrder is non-negative.
/// </summary>
internal sealed class AddDocumentToCollectionCommandValidator : AbstractValidator<AddDocumentToCollectionCommand>
{
    public AddDocumentToCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty()
            .WithMessage("CollectionId is required.");

        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PdfDocumentId is required.");

        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("DocumentType is required (max 100 chars).");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("SortOrder cannot be negative.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
