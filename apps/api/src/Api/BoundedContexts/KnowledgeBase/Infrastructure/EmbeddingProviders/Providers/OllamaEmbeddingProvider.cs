using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;

/// <summary>
/// Ollama embedding provider supporting nomic-embed-text and mxbai-embed-large.
/// Uses local Ollama server for free, privacy-preserving embeddings.
/// </summary>
public sealed class OllamaEmbeddingProvider : EmbeddingProviderBase
{
    private readonly EmbeddingProviderType _providerType;
    private readonly string _modelName;
    private readonly int _dimensions;

    public OllamaEmbeddingProvider(
        HttpClient httpClient,
        ILogger<OllamaEmbeddingProvider> logger,
        EmbeddingConfiguration config,
        EmbeddingProviderType providerType)
        : base(httpClient, logger, config)
    {
        if (providerType != EmbeddingProviderType.OllamaNomic &&
            providerType != EmbeddingProviderType.OllamaMxbai)
        {
            throw new ArgumentException(
                $"OllamaEmbeddingProvider only supports OllamaNomic or OllamaMxbai, got {providerType}",
                nameof(providerType));
        }

        _providerType = providerType;
        _modelName = config.Model ?? providerType.GetModelName();
        _dimensions = config.Dimensions ?? providerType.GetDimensions();

        // Configure HttpClient for Ollama
        var ollamaUrl = config.OllamaUrl ?? "http://localhost:11434";
        HttpClient.BaseAddress = new Uri(ollamaUrl);
        HttpClient.Timeout = TimeSpan.FromSeconds(Math.Max(config.TimeoutSeconds, 60)); // Ollama may need longer timeout
    }

    public override string ProviderName => "Ollama";
    public override string ModelName => _modelName;
    public override int Dimensions => _dimensions;
    public override int MaxContextTokens => _providerType.GetMaxContextTokens();

    public override async Task<EmbeddingProviderResult> GenerateBatchEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return EmbeddingProviderResult.CreateFailure("No texts provided");
        }

        Logger.LogInformation(
            "Generating embeddings for {Count} texts using Ollama model {Model}",
            texts.Count, _modelName);

        try
        {
            var embeddings = new List<float[]>();

            // Ollama /api/embeddings processes one text at a time
            foreach (var text in texts)
            {
                var result = await GenerateSingleEmbeddingAsync(text, ct).ConfigureAwait(false);
                if (!result.Success)
                {
                    return result;
                }

                embeddings.AddRange(result.Embeddings);
            }

            Logger.LogInformation(
                "Successfully generated {Count} embeddings via Ollama",
                embeddings.Count);

            return EmbeddingProviderResult.CreateSuccess(embeddings, _modelName);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error calling Ollama API");
            return EmbeddingProviderResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != ct)
        {
            Logger.LogError(ex, "Timeout calling Ollama API");
            return EmbeddingProviderResult.CreateFailure("Request timeout");
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "JSON parsing error from Ollama API");
            return EmbeddingProviderResult.CreateFailure($"Invalid response format: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error calling Ollama API");
            return EmbeddingProviderResult.CreateFailure($"Unexpected error: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    private async Task<EmbeddingProviderResult> GenerateSingleEmbeddingAsync(
        string text,
        CancellationToken ct)
    {
        var request = new OllamaEmbeddingRequest
        {
            Model = _modelName,
            Prompt = text
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // CODE-01: Dispose HttpResponseMessage to prevent resource leak (CWE-404)
        using var response = await HttpClient.PostAsync("/api/embeddings", content, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError(
                "Ollama API error: {Status} - {Body}",
                response.StatusCode, responseBody);

            // Check if model needs to be pulled
            if (responseBody.Contains("model", StringComparison.OrdinalIgnoreCase) &&
                responseBody.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return EmbeddingProviderResult.CreateFailure($"Model '{_modelName}' not found. Run: ollama pull {_modelName}");
            }

            return EmbeddingProviderResult.CreateFailure($"API error: {(int)response.StatusCode}");
        }

        var ollamaResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseBody);

        if (ollamaResponse?.Embedding == null || ollamaResponse.Embedding.Length == 0)
        {
            return EmbeddingProviderResult.CreateFailure("No embedding returned from Ollama");
        }

        // Validate embedding
        if (!ValidateEmbeddingDimensions(ollamaResponse.Embedding))
        {
            return EmbeddingProviderResult.CreateFailure("Invalid embedding dimensions received");
        }

        return EmbeddingProviderResult.CreateSuccess(ollamaResponse.Embedding, _modelName);
    }

    public override async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            // Check Ollama API availability
            using var response = await HttpClient.GetAsync("/api/tags", ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Ollama server not responding: {Status}", response.StatusCode);
                return false;
            }

            // Verify the model is available
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(responseBody);

            var modelAvailable = tagsResponse?.Models?.Any(m =>
                m.Name?.Contains(_modelName, StringComparison.OrdinalIgnoreCase) == true) ?? false;

            if (!modelAvailable)
            {
                Logger.LogWarning("Ollama model {Model} not available", _modelName);
                return false;
            }

            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Ollama health check failed");
            return false;
        }
#pragma warning restore CA1031
    }
}

// Request/Response models for Ollama API
internal sealed record OllamaEmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;
}

internal sealed record OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = Array.Empty<float>();
}

internal sealed record OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo>? Models { get; init; }
}

internal sealed record OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("modified_at")]
    public string? ModifiedAt { get; init; }
}
