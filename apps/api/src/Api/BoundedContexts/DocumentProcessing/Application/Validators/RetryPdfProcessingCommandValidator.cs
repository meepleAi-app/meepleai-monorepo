using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for RetryPdfProcessingCommand.
/// Issue #4216: PDF Pipeline - Error Handling and Manual Retry
/// </summary>
internal sealed class RetryPdfProcessingCommandValidator : AbstractValidator<RetryPdfProcessingCommand>
{
    public RetryPdfProcessingCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");
    }
}
