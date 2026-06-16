using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Shared FluentValidation predicates for translation commands and queries.
/// Issue #2379 (F1) — consolidates <c>BeValidLocale</c> previously duplicated in
/// <c>Add/Update/DeleteGameTranslationCommandValidator</c>.
/// </summary>
internal static class SharedTranslationValidationRules
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="raw"/> parses as a normalised
    /// ISO 639-1 locale (optionally with an ISO 3166-1 regional suffix). Wraps
    /// <see cref="Locale.Create(string)"/> to translate
    /// <see cref="InvalidLocaleException"/> into a boolean rule predicate
    /// suitable for <c>RuleFor(x => x.Locale).Must(BeValidLocale)</c>.
    /// </summary>
    public static bool BeValidLocale(string? raw)
    {
        if (raw is null)
        {
            return false;
        }

        try
        {
            Locale.Create(raw);
            return true;
        }
        catch (InvalidLocaleException)
        {
            return false;
        }
    }
}
