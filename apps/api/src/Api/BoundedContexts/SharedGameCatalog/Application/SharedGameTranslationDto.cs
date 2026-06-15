namespace Api.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Lightweight DTO for surfacing a single localized translation alongside the canonical
/// EN copy on <see cref="SharedGameDto"/>. Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// <see cref="Source"/> holds the kebab-case persisted form ("manual",
/// "auto-openrouter", "community") so consumers don't need to take a dependency on the
/// <c>TranslationSource</c> domain enum. Conversion is centralised in
/// <c>TranslationSourceMapper</c>.
/// </remarks>
public sealed record SharedGameTranslationDto(
    string Locale,
    string Title,
    string? Description,
    string Source);
