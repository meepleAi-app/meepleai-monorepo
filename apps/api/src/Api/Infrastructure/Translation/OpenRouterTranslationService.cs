using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Translation;

internal sealed class OpenRouterTranslationService(
    ILlmService llmService,
    ILogger<OpenRouterTranslationService> logger)
    : INarrativeTranslationService, IGenericTranslationService
{
    private const string NarrativeModel = "anthropic/claude-sonnet-4-5";
    private const string GenericModel = "deepseek/deepseek-chat";
    private const decimal MaxRequestCostUsd = 0.05m; // R-18 guard: static ceiling

    public async Task<TranslationResult> TranslateNarrativeAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string? gameContext,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        var context = gameContext is not null
            ? $"This is narrative text from the board game '{gameContext}'. Preserve character names, place names, and game terms. "
            : string.Empty;

        var systemPrompt =
            $"{context}You are a professional literary translator specializing in board game rulebooks. " +
            $"Translate the following text from {sourceLanguage} to {targetLanguage}. " +
            "Preserve tone, atmosphere, and game-specific terminology. Output ONLY the translation, no explanations.";

        try
        {
            var result = await llmService.GenerateCompletionWithModelAsync(
                NarrativeModel, systemPrompt, text,
                RequestSource.Translation, maxTokens: 2048, ct).ConfigureAwait(false);

            if (!result.Success)
                return TranslationResult.CreateFailure(sourceLanguage, targetLanguage, "LLM_FAILURE", result.ErrorMessage ?? "Unknown");

            if (result.Cost.TotalCost > MaxRequestCostUsd)
                logger.LogWarning(
                    "[Translation] Cost ceiling exceeded: {Cost:F6} USD > {Ceiling:F6} USD",
                    result.Cost.TotalCost, MaxRequestCostUsd);

            logger.LogInformation(
                "[Translation] Narrative: {SrcLang}→{TgtLang}, cost={Cost:F6} USD, length={Len}",
                sourceLanguage, targetLanguage, result.Cost.TotalCost, text.Length);

            return TranslationResult.CreateSuccess(result.Response, sourceLanguage, targetLanguage, result.Cost.TotalCost);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Translation] Narrative translation failed");
            return TranslationResult.CreateFailure(sourceLanguage, targetLanguage, "EXCEPTION", ex.Message);
        }
    }

    public async Task<TranslationResult> TranslateGenericAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var systemPrompt =
            $"Translate the following text from {sourceLanguage} to {targetLanguage}. " +
            "Output ONLY the translation, no explanations, no quotation marks.";

        try
        {
            var result = await llmService.GenerateCompletionWithModelAsync(
                GenericModel, systemPrompt, text,
                RequestSource.Translation, maxTokens: 512, ct).ConfigureAwait(false);

            if (!result.Success)
                return TranslationResult.CreateFailure(sourceLanguage, targetLanguage, "LLM_FAILURE", result.ErrorMessage ?? "Unknown");

            return TranslationResult.CreateSuccess(result.Response, sourceLanguage, targetLanguage, result.Cost.TotalCost);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Translation] Generic translation failed");
            return TranslationResult.CreateFailure(sourceLanguage, targetLanguage, "EXCEPTION", ex.Message);
        }
    }
}
