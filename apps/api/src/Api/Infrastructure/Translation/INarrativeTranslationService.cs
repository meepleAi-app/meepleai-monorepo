namespace Api.Infrastructure.Translation;

/// <summary>
/// Translates multi-paragraph narrative game text with creative fidelity.
/// Uses premium LLM (Claude Sonnet / GPT-4o) via OpenRouter.
/// Suitable for gamebook paragraphs, story segments, NPC dialogue.
/// </summary>
internal interface INarrativeTranslationService
{
    /// <param name="text">Source text (paragraph or multi-paragraph)</param>
    /// <param name="sourceLanguage">ISO 639-1 source language code (e.g., "en")</param>
    /// <param name="targetLanguage">ISO 639-1 target language code (e.g., "it")</param>
    /// <param name="gameContext">Optional game name for proper noun context</param>
    /// <param name="ct">Cancellation token</param>
    Task<TranslationResult> TranslateNarrativeAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string? gameContext,
        CancellationToken ct = default);
}
