using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for DeletePdfCommand.
/// Ensures PdfId is a non-empty string.
/// </summary>
internal sealed class DeletePdfCommandValidator : AbstractValidator<DeletePdfCommand>
{
    public DeletePdfCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PdfId is required.");
    }
}
