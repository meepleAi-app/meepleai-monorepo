using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for ReindexDocumentCommand.
/// Validates GUID properties are non-empty.
/// </summary>
internal sealed class ReindexDocumentCommandValidator : AbstractValidator<ReindexDocumentCommand>
{
    public ReindexDocumentCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");
    }
}
