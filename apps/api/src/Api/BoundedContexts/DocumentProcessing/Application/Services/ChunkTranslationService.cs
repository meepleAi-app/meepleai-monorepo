using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Translates document chunks from a source language to a target language through the LLM gateway.
/// Used in the RAG pipeline to enable cross-language retrieval by translating non-English chunks to English.
/// </summary>
/// <remarks>
/// #4165 (ADR-095 D4): the call goes through <see cref="ILlmService"/> with an explicit model, so it is
/// cost-logged under <see cref="RequestSource.ChunkTranslation"/>, uses the admin-managed provider
/// credential and the provider circuit breaker. Two settings now come from the gateway instead of
/// this class: the temperature (gateway default, was 0.1; per-call temperature arrives with ADR-095
/// slice 5) and the timeout (the OpenRouter client's, was 120s here).
/// </remarks>
internal sealed partial class ChunkTranslationService : IChunkTranslationService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<ChunkTranslationService> _logger;
    private readonly string _model;
    private readonly int _batchSize;

    // #4179: «anthropic/claude-3-haiku» non è più nel listino di OpenRouter (verificato il
    // 2026-10-10). Il sostituto è nel catalogo, con prezzo verificato.
    private const string DefaultModel = "meta-llama/llama-3.3-70b-instruct";
    private const int DefaultBatchSize = 5;
    private const int MaxTokens = 4096;

    public ChunkTranslationService(
        ILlmService llmService,
        IConfiguration configuration,
        ILogger<ChunkTranslationService> logger)
    {
        _llmService = llmService;
        _logger = logger;

        _model = configuration.GetValue<string>("RagTranslation:Model") ?? DefaultModel;
        _batchSize = configuration.GetValue<int?>("RagTranslation:BatchSize") ?? DefaultBatchSize;

        _logger.LogInformation(
            "ChunkTranslationService initialized (model={Model}, batchSize={BatchSize})",
            _model, _batchSize);
    }

    public async Task<List<TranslatedChunk>> TranslateChunksAsync(
        IReadOnlyList<string> chunks,
        string sourceLanguage,
        string targetLanguage = "en",
        CancellationToken ct = default)
    {
        if (chunks.Count == 0)
        {
            return [];
        }

        _logger.LogInformation(
            "Translating {Count} chunks from {Source} to {Target}",
            chunks.Count, sourceLanguage, targetLanguage);

        var results = new List<TranslatedChunk>();

        // Process in batches
        for (var batchStart = 0; batchStart < chunks.Count; batchStart += _batchSize)
        {
            var batchEnd = Math.Min(batchStart + _batchSize, chunks.Count);
            var batchChunks = chunks.Skip(batchStart).Take(batchEnd - batchStart).ToList();

            try
            {
                var batchResults = await TranslateBatchAsync(
                    batchChunks, batchStart, sourceLanguage, targetLanguage, ct).ConfigureAwait(false);
                results.AddRange(batchResults);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to translate batch starting at index {BatchStart}, skipping {Count} chunks",
                    batchStart, batchChunks.Count);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation(
            "Translation complete: {Translated}/{Total} chunks translated",
            results.Count, chunks.Count);

        return results;
    }

    private async Task<List<TranslatedChunk>> TranslateBatchAsync(
        List<string> batchChunks,
        int globalOffset,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct)
    {
        var systemPrompt = $"You are a professional translator specializing in board game rulebooks. " +
            $"Translate the following text from {sourceLanguage} to {targetLanguage}. " +
            $"Preserve all formatting, numbering, and game-specific terminology exactly. " +
            $"Return ONLY the translation, nothing else.";

        var userPrompt = BuildBatchUserPrompt(batchChunks);

        var result = await _llmService.GenerateCompletionWithModelAsync(
            _model, systemPrompt, userPrompt,
            RequestSource.ChunkTranslation, maxTokens: MaxTokens, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            // Same outcome as before the gateway: the batch is skipped, the others go on.
            _logger.LogWarning(
                "Translation batch at offset {Offset} failed in the LLM gateway: {Error}",
                globalOffset, result.ErrorMessage);
            return [];
        }

        var assistantMessage = result.Response;

        if (string.IsNullOrWhiteSpace(assistantMessage))
        {
            _logger.LogWarning("Empty response from the LLM gateway for translation batch at offset {Offset}", globalOffset);
            return [];
        }

        return ParseBatchResponse(assistantMessage, batchChunks, globalOffset, sourceLanguage, targetLanguage);
    }

    private static string BuildBatchUserPrompt(List<string> chunks)
    {
        if (chunks.Count == 1)
        {
            return chunks[0];
        }

        var sb = new StringBuilder();
        sb.AppendLine("Translate each section below:");
        sb.AppendLine();

        for (var i = 0; i < chunks.Count; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"[{i + 1}]");
            sb.AppendLine(chunks[i]);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private List<TranslatedChunk> ParseBatchResponse(
        string response,
        List<string> originalChunks,
        int globalOffset,
        string sourceLanguage,
        string targetLanguage)
    {
        var results = new List<TranslatedChunk>();

        if (originalChunks.Count == 1)
        {
            // Single chunk: the entire response is the translation
            results.Add(new TranslatedChunk(
                OriginalIndex: globalOffset,
                OriginalText: originalChunks[0],
                TranslatedText: response.Trim(),
                SourceLanguage: sourceLanguage,
                TargetLanguage: targetLanguage));
            return results;
        }

        // Multi-chunk: split by [1], [2], etc.
        var sections = BatchSectionRegex().Split(response);

        // First element is text before [1] (usually empty), skip it
        // Subsequent elements alternate: section number, section content
        for (var i = 1; i < sections.Length; i += 2)
        {
            if (!int.TryParse(sections[i], CultureInfo.InvariantCulture, out var sectionNum) || sectionNum < 1 || sectionNum > originalChunks.Count)
            {
                continue;
            }

            var chunkIndex = sectionNum - 1;
            var translatedText = i + 1 < sections.Length ? sections[i + 1].Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(translatedText))
            {
                _logger.LogWarning(
                    "Empty translation for section [{Section}] at global index {Index}",
                    sectionNum, globalOffset + chunkIndex);
                continue;
            }

            results.Add(new TranslatedChunk(
                OriginalIndex: globalOffset + chunkIndex,
                OriginalText: originalChunks[chunkIndex],
                TranslatedText: translatedText,
                SourceLanguage: sourceLanguage,
                TargetLanguage: targetLanguage));
        }

        if (results.Count < originalChunks.Count)
        {
            _logger.LogWarning(
                "Parsed {Parsed}/{Expected} translations from batch at offset {Offset}",
                results.Count, originalChunks.Count, globalOffset);
        }

        return results;
    }

    [GeneratedRegex(@"\[(?<num>\d+)\]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BatchSectionRegex();
}
