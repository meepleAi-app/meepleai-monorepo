using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators.Queue;

/// <summary>
/// Validator for EnqueuePdfCommand.
/// Issue #4731: Queue command validation.
/// </summary>
internal sealed class EnqueuePdfCommandValidator : AbstractValidator<EnqueuePdfCommand>
{
    public EnqueuePdfCommandValidator()
    {
        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PDF document ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.Priority)
            .InclusiveBetween(0, 100)
            .WithMessage("Priority must be between 0 and 100.");
    }
}
