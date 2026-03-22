using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for SetPdfVisibilityCommand.
/// Validates GUID properties are non-empty.
/// </summary>
internal sealed class SetPdfVisibilityCommandValidator : AbstractValidator<SetPdfVisibilityCommand>
{
    public SetPdfVisibilityCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");
    }
}
