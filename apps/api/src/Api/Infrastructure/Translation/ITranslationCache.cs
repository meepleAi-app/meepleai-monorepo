namespace Api.Infrastructure.Translation;

internal interface ITranslationCache
{
    Task<TranslationResult?> TryGetAsync(
        Guid gameId,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default);

    Task SetAsync(
        Guid gameId,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        TranslationResult result,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default);
}
