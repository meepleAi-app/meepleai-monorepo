using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for IndexPdfCommand.
/// Ensures PdfId is a non-empty string.
/// </summary>
internal sealed class IndexPdfCommandValidator : AbstractValidator<IndexPdfCommand>
{
    public IndexPdfCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PdfId is required.");
    }
}
