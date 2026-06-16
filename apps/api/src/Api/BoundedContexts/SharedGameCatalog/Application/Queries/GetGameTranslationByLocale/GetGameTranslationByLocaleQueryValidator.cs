using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslationByLocale;

/// <summary>
/// Validator for <see cref="GetGameTranslationByLocaleQuery"/>. Issue #2379 (F5).
/// </summary>
/// <remarks>
/// Without this validator the handler's call to <c>Locale.Create(query.Locale)</c>
/// throws <c>InvalidLocaleException</c> (an <c>ArgumentException</c>), which the
/// global exception middleware maps to HTTP 400 with the generic
/// "Invalid request parameters" body — losing the locale-specific detail. Routing
/// the check through FluentValidation surfaces the more informative
/// "Invalid ISO 639-1 locale" message via HTTP 422.
/// </remarks>
public sealed class GetGameTranslationByLocaleQueryValidator
    : AbstractValidator<GetGameTranslationByLocaleQuery>
{
    public GetGameTranslationByLocaleQueryValidator()
    {
        RuleFor(q => q.GameId).NotEmpty();

        RuleFor(q => q.Locale)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(SharedTranslationValidationRules.BeValidLocale)
            .WithMessage("Invalid ISO 639-1 locale");
    }
}
