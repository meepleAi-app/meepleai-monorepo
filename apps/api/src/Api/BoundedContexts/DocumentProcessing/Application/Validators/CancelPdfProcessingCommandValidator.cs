using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for CancelPdfProcessingCommand.
/// Validates GUID properties are non-empty.
/// </summary>
internal sealed class CancelPdfProcessingCommandValidator : AbstractValidator<CancelPdfProcessingCommand>
{
    public CancelPdfProcessingCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");
    }
}
