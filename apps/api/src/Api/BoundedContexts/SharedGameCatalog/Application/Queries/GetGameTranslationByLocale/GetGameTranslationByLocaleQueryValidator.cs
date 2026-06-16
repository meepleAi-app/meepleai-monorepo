using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslationByLocale;

/// <summary>
/// Validator for <see cref="GetGameTranslationByLocaleQuery"/>. Issue #2379 (F5).
/// </summary>
/// <remarks>
/// Issue #2399 update: the handler now calls <c>Locale.TryCreate</c> and
/// degrades gracefully to <c>null</c> on bypass (mapped to HTTP 404 by the
/// endpoint, same shape as "no row matches"). Routing the locale check
/// through FluentValidation here surfaces the more informative
/// "Invalid ISO 639-1 locale" message via HTTP 422 instead of the
/// indistinguishable 404, so callers can tell "bad input" from "missing row".
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
