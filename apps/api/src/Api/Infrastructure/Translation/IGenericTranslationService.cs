namespace Api.Infrastructure.Translation;

/// <summary>
/// Translates short factual strings: error messages, UI labels, QA answer snippets.
/// Uses lower-cost model (DeepSeek V3 / Claude Haiku) via OpenRouter.
/// </summary>
internal interface IGenericTranslationService
{
    /// <param name="text">Short text to translate (sentence-level, no creative latitude)</param>
    /// <param name="sourceLanguage">ISO 639-1 source language code (e.g., "en")</param>
    /// <param name="targetLanguage">ISO 639-1 target language code (e.g., "it")</param>
    /// <param name="ct">Cancellation token</param>
    Task<TranslationResult> TranslateGenericAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default);
}
