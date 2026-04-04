using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for OverridePdfLanguageCommand.
/// E5-2: Language Intelligence for Game Night Improvvisata.
/// </summary>
internal sealed class OverridePdfLanguageCommandValidator : AbstractValidator<OverridePdfLanguageCommand>
{
    public OverridePdfLanguageCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");

        RuleFor(x => x.LanguageCode)
            .MaximumLength(3)
            .WithMessage("Language code must be 2-3 characters (ISO 639-1/2).")
            .MinimumLength(2)
            .WithMessage("Language code must be 2-3 characters (ISO 639-1/2).")
            .When(x => x.LanguageCode is not null);
    }
}
