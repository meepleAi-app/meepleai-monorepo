using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for ReclassifyDocumentCommand.
/// Ensures PdfId is non-empty, Category is valid, and VersionLabel length is bounded.
/// </summary>
internal sealed class ReclassifyDocumentCommandValidator : AbstractValidator<ReclassifyDocumentCommand>
{
    public ReclassifyDocumentCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PdfId is required.");

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Category is required (max 100 chars).");

        RuleFor(x => x.VersionLabel)
            .MaximumLength(100)
            .When(x => x.VersionLabel != null)
            .WithMessage("VersionLabel cannot exceed 100 characters.");
    }
}
