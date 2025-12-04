using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;

/// <summary>
/// HuggingFace embedding provider supporting BGE-M3 model.
/// Uses HuggingFace Inference API for multilingual embeddings.
/// </summary>
public sealed class HuggingFaceEmbeddingProvider : EmbeddingProviderBase
{
    private readonly string _modelName;
    private readonly int _dimensions;
    private readonly string? _apiKey;

    public HuggingFaceEmbeddingProvider(
        HttpClient httpClient,
        ILogger<HuggingFaceEmbeddingProvider> logger,
        EmbeddingConfiguration config)
        : base(httpClient, logger, config)
    {
        // BGAI-081: Handle both null and empty string from config binding
        _modelName = !string.IsNullOrWhiteSpace(config.Model) ? config.Model : EmbeddingProviderType.HuggingFaceBgeM3.GetModelName();
        _dimensions = config.Dimensions ?? EmbeddingProviderType.HuggingFaceBgeM3.GetDimensions();
        _apiKey = config.HuggingFaceApiKey;

        // NOTE: HttpClient BaseAddress and Timeout are configured in
        // InfrastructureServiceExtensions.AddHttpClients() to avoid conflicts with IHttpClientFactory.
        // API key is passed per-request via Authorization header to ensure thread-safety.
    }

    public override string ProviderName => "HuggingFace";
    public override string ModelName => _modelName;
    public override int Dimensions => _dimensions;
    public override int MaxContextTokens => EmbeddingProviderType.HuggingFaceBgeM3.GetMaxContextTokens();

    public override async Task<EmbeddingProviderResult> GenerateBatchEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return EmbeddingProviderResult.CreateFailure("No texts provided");
        }

        Logger.LogInformation(
            "Generating embeddings for {Count} texts using HuggingFace model {Model}",
            texts.Count, _modelName);

        try
        {
            var allEmbeddings = new List<float[]>();

            // Process in batches
            foreach (var batch in BatchTexts(texts))
            {
                var result = await GenerateBatchInternalAsync(batch, ct).ConfigureAwait(false);
                if (!result.Success)
                {
                    return result;
                }

                allEmbeddings.AddRange(result.Embeddings);
            }

            Logger.LogInformation(
                "Successfully generated {Count} embeddings via HuggingFace",
                allEmbeddings.Count);

            return EmbeddingProviderResult.CreateSuccess(allEmbeddings, _modelName);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error calling HuggingFace API");
            return EmbeddingProviderResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != ct)
        {
            Logger.LogError(ex, "Timeout calling HuggingFace API");
            return EmbeddingProviderResult.CreateFailure("Request timeout");
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "JSON parsing error from HuggingFace API");
            return EmbeddingProviderResult.CreateFailure($"Invalid response format: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error calling HuggingFace API");
            return EmbeddingProviderResult.CreateFailure($"Unexpected error: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    private async Task<EmbeddingProviderResult> GenerateBatchInternalAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct)
    {
        var request = new HuggingFaceEmbeddingRequest
        {
            Inputs = texts.ToList(),
            Options = new HuggingFaceOptions
            {
                WaitForModel = true
            }
        };

        var json = JsonSerializer.Serialize(request, JsonSerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Create request with per-request Authorization header for thread-safety
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = content
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        // CODE-01: Dispose HttpResponseMessage to prevent resource leak (CWE-404)
        using var response = await HttpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError(
                "HuggingFace API error: {Status} - {Body}",
                response.StatusCode, responseBody);

            // Check for rate limiting
            if ((int)response.StatusCode == 429)
            {
                return EmbeddingProviderResult.CreateFailure("Rate limit exceeded");
            }

            // Check for model loading
            if ((int)response.StatusCode == 503)
            {
                return EmbeddingProviderResult.CreateFailure("Model is loading, please retry");
            }

            return EmbeddingProviderResult.CreateFailure($"API error: {(int)response.StatusCode}");
        }

        // HuggingFace returns array of embeddings directly
        var embeddings = ParseEmbeddingsResponse(responseBody);

        if (embeddings == null || embeddings.Count == 0)
        {
            return EmbeddingProviderResult.CreateFailure("No embeddings returned from HuggingFace");
        }

        // Validate embeddings
        foreach (var embedding in embeddings)
        {
            if (!ValidateEmbeddingDimensions(embedding))
            {
                return EmbeddingProviderResult.CreateFailure("Invalid embedding dimensions received");
            }
        }

        return EmbeddingProviderResult.CreateSuccess(embeddings, _modelName);
    }

    private List<float[]>? ParseEmbeddingsResponse(string responseBody)
    {
        try
        {
            // HuggingFace feature-extraction returns nested arrays
            // Response format: [[embedding1], [embedding2], ...]
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var embeddings = new List<float[]>();

                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Array)
                    {
                        // FIX: Check for empty array to prevent IndexOutOfRangeException
                        if (element.GetArrayLength() == 0)
                        {
                            continue;
                        }

                        // Check if this is a nested array (batch) or direct embedding
                        var firstElement = element[0];
                        if (firstElement.ValueKind == JsonValueKind.Array)
                        {
                            // Nested array - take the first (CLS token or mean pooled)
                            var embedding = firstElement.EnumerateArray()
                                .Select(e => e.GetSingle())
                                .ToArray();
                            embeddings.Add(embedding);
                        }
                        else
                        {
                            // Direct embedding array
                            var embedding = element.EnumerateArray()
                                .Select(e => e.GetSingle())
                                .ToArray();
                            embeddings.Add(embedding);
                        }
                    }
                }

                return embeddings;
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse HuggingFace embeddings response");
            return null;
        }
    }

    public override async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            // Simple health check with minimal text
            var result = await GenerateEmbeddingAsync("test", ct).ConfigureAwait(false);
            return result.Success;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "HuggingFace health check failed");
            return false;
        }
#pragma warning restore CA1031
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

// Request models for HuggingFace API
internal sealed record HuggingFaceEmbeddingRequest
{
    [JsonPropertyName("inputs")]
    public List<string> Inputs { get; init; } = new();

    [JsonPropertyName("options")]
    public HuggingFaceOptions? Options { get; init; }
}

internal sealed record HuggingFaceOptions
{
    [JsonPropertyName("wait_for_model")]
    public bool WaitForModel { get; init; }

    [JsonPropertyName("use_cache")]
    public bool UseCache { get; init; } = true;
}
