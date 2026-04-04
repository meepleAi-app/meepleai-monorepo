using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for ExtractPdfTextCommand.
/// Validates GUID properties are non-empty.
/// </summary>
internal sealed class ExtractPdfTextCommandValidator : AbstractValidator<ExtractPdfTextCommand>
{
    public ExtractPdfTextCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");
    }
}
