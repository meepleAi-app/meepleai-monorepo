using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Api.Infrastructure;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Translates document chunks from a source language to a target language using OpenRouter's LLM API.
/// Used in the RAG pipeline to enable cross-language retrieval by translating non-English chunks to English.
/// </summary>
internal sealed partial class ChunkTranslationService : IChunkTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChunkTranslationService> _logger;
    private readonly string _model;
    private readonly int _batchSize;

    private const string DefaultModel = "anthropic/claude-3-haiku";
    private const int DefaultBatchSize = 5;
    private const double Temperature = 0.1;
    private const int MaxTokens = 4096;

    public ChunkTranslationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ChunkTranslationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ChunkTranslation");
        _logger = logger;

        _model = configuration.GetValue<string>("RagTranslation:Model") ?? DefaultModel;
        _batchSize = configuration.GetValue<int?>("RagTranslation:BatchSize") ?? DefaultBatchSize;

#pragma warning disable S1075 // URIs should not be hardcoded - Official API endpoint
        const string openRouterApiBaseUrl = "https://openrouter.ai/api/v1/";
#pragma warning restore S1075

        var apiKey = SecretsHelper.GetSecretOrValue(configuration, "OPENROUTER_API_KEY", logger, required: true)
            ?? throw new InvalidOperationException("OPENROUTER_API_KEY not configured");

        _httpClient.BaseAddress = new Uri(openRouterApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://meepleai.app");
        _httpClient.Timeout = TimeSpan.FromSeconds(120);

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

        var requestPayload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["model"] = _model,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            ["temperature"] = Temperature,
            ["max_tokens"] = MaxTokens
        };

        var json = JsonSerializer.Serialize(requestPayload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenRouter translation API error: {Status} - {Body}",
                response.StatusCode, responseBody);
            throw new HttpRequestException(
                $"OpenRouter API returned {(int)response.StatusCode} ({response.StatusCode})");
        }

        var chatResponse = JsonSerializer.Deserialize<TranslationChatResponse>(responseBody);
        var assistantMessage = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(assistantMessage))
        {
            _logger.LogWarning("Empty response from OpenRouter for translation batch at offset {Offset}", globalOffset);
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

    // Minimal response models for translation (reuses OpenRouter chat/completions format)
    private sealed record TranslationChatResponse
    {
        [JsonPropertyName("choices")]
        public List<TranslationChoice>? Choices { get; init; }
    }

    private sealed record TranslationChoice
    {
        [JsonPropertyName("message")]
        public TranslationMessage? Message { get; init; }
    }

    private sealed record TranslationMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }
}
