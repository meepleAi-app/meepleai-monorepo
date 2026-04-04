using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for SetActiveForRagCommand.
/// Validates GUID properties are non-empty.
/// </summary>
internal sealed class SetActiveForRagCommandValidator : AbstractValidator<SetActiveForRagCommand>
{
    public SetActiveForRagCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");
    }
}
