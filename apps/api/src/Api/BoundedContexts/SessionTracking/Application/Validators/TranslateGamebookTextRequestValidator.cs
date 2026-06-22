using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Translation;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Validators;

/// <summary>
/// Validates the manual text translation request per DEC-BE-4 (#1774).
/// Text: 1-2000 chars, non-whitespace. SourceLang: EN|FR|DE|ES|IT (case-insensitive).
/// TargetLang: IT only (fixed v1). GameBookId: non-empty.
/// </summary>
public sealed class TranslateGamebookTextRequestValidator : AbstractValidator<TranslateGamebookTextRequest>
{
    public TranslateGamebookTextRequestValidator()
    {
        RuleFor(r => r.Text)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Text must not be empty.")
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Text must not be whitespace only.")
            .Length(1, 2000).WithMessage("Text length must be between 1 and 2000 characters.");

        RuleFor(r => r.SourceLang)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("SourceLang is required.")
            .Must(LanguageCodes.IsValidSourceLang).WithMessage("SourceLang must be one of EN, FR, DE, ES, IT.");

        RuleFor(r => r.TargetLang)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("TargetLang is required.")
            .Must(tl => string.Equals(tl, "IT", StringComparison.OrdinalIgnoreCase))
            .WithMessage("TargetLang must equal IT (fixed v1).");

        RuleFor(r => r.GameBookId).NotEmpty();
    }
}
