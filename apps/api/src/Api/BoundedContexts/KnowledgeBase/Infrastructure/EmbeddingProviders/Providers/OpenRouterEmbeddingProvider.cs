using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;

/// <summary>
/// OpenRouter embedding provider supporting text-embedding-3-large and text-embedding-3-small.
/// Uses OpenAI-compatible API via OpenRouter gateway.
/// </summary>
internal sealed class OpenRouterEmbeddingProvider : EmbeddingProviderBase
{
    private readonly EmbeddingProviderType _providerType;
    private readonly string _modelName;
    private readonly int _dimensions;
    private readonly string? _apiKey;

    public OpenRouterEmbeddingProvider(
        HttpClient httpClient,
        ILogger<OpenRouterEmbeddingProvider> logger,
        EmbeddingConfiguration config,
        EmbeddingProviderType providerType)
        : base(httpClient, logger, config)
    {
        if (providerType != EmbeddingProviderType.OpenRouterLarge &&
            providerType != EmbeddingProviderType.OpenRouterSmall)
        {
            throw new ArgumentException(
                $"OpenRouterEmbeddingProvider only supports OpenRouterLarge or OpenRouterSmall, got {providerType}",
                nameof(providerType));
        }

        _providerType = providerType;
        // BGAI-081: Handle both null and empty string from config binding
        _modelName = !string.IsNullOrWhiteSpace(config.Model) ? config.Model : providerType.GetModelName();
        _dimensions = config.Dimensions ?? providerType.GetDimensions();
        _apiKey = config.OpenRouterApiKey;

        // NOTE: HttpClient BaseAddress and Timeout are configured in
        // InfrastructureServiceExtensions.AddHttpClients() to avoid conflicts with IHttpClientFactory.
        // API key is passed per-request via Authorization header to ensure thread-safety.
    }

    public override string ProviderName => "OpenRouter";
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
            "Generating embeddings for {Count} texts using OpenRouter model {Model}",
            texts.Count, _modelName);

        try
        {
            var allEmbeddings = new List<float[]>();
            var totalTokens = 0;

            // Process in batches if needed
            foreach (var batch in BatchTexts(texts))
            {
                var result = await GenerateBatchInternalAsync(batch, ct).ConfigureAwait(false);
                if (!result.Success)
                {
                    return result;
                }

                allEmbeddings.AddRange(result.Embeddings);
                totalTokens += result.TokensUsed ?? 0;
            }

            Logger.LogInformation(
                "Successfully generated {Count} embeddings via OpenRouter (tokens used: {Tokens})",
                allEmbeddings.Count, totalTokens);

            return EmbeddingProviderResult.CreateSuccess(allEmbeddings, _modelName, totalTokens);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps OpenRouter API exceptions (HTTP, JSON, timeout) into domain-friendly EmbeddingProviderResult
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error calling OpenRouter API");
            return EmbeddingProviderResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != ct)
        {
            Logger.LogError(ex, "Timeout calling OpenRouter API");
            return EmbeddingProviderResult.CreateFailure("Request timeout");
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "JSON parsing error from OpenRouter API");
            return EmbeddingProviderResult.CreateFailure($"Invalid response format: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error calling OpenRouter API");
            return EmbeddingProviderResult.CreateFailure($"Unexpected error: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    private async Task<EmbeddingProviderResult> GenerateBatchInternalAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct)
    {
        var request = new OpenRouterEmbeddingRequest
        {
            Model = _modelName,
            Input = texts.ToList(),
            EncodingFormat = "float"
        };

        var json = JsonSerializer.Serialize(request, JsonSerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Create request with per-request Authorization header for thread-safety
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "embeddings")
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
                "OpenRouter API error: {Status} - {Body}",
                response.StatusCode, responseBody);

            // Check for rate limiting
            if ((int)response.StatusCode == 429)
            {
                return EmbeddingProviderResult.CreateFailure("Rate limit exceeded");
            }

            return EmbeddingProviderResult.CreateFailure($"API error: {(int)response.StatusCode}");
        }

        var embeddingResponse = JsonSerializer.Deserialize<OpenRouterEmbeddingResponse>(responseBody, JsonSerializerOptions);

        if (embeddingResponse?.Data == null || embeddingResponse.Data.Count == 0)
        {
            return EmbeddingProviderResult.CreateFailure("No embeddings returned from OpenRouter");
        }

        // Sort by index and extract embeddings
        var embeddings = embeddingResponse.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();

        // Validate embeddings
        foreach (var embedding in embeddings)
        {
            if (!ValidateEmbeddingDimensions(embedding))
            {
                return EmbeddingProviderResult.CreateFailure("Invalid embedding dimensions received");
            }
        }

        return EmbeddingProviderResult.CreateSuccess(
            embeddings,
            embeddingResponse.Model,
            embeddingResponse.Usage?.TotalTokens);
    }

    public override async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            // Quick health check with minimal text
            var result = await GenerateEmbeddingAsync("test", ct).ConfigureAwait(false);
            return result.Success;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - Health checks must not propagate exceptions, return false to indicate unhealthy state
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "OpenRouter health check failed");
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

// Request/Response models for OpenRouter API
internal sealed record OpenRouterEmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("input")]
    public List<string> Input { get; init; } = new();

    [JsonPropertyName("encoding_format")]
    public string EncodingFormat { get; init; } = "float";
}

internal sealed record OpenRouterEmbeddingResponse
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public List<OpenRouterEmbeddingData> Data { get; init; } = new();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("usage")]
    public OpenRouterUsage? Usage { get; init; }
}

internal sealed record OpenRouterEmbeddingData
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = Array.Empty<float>();

    [JsonPropertyName("index")]
    public int Index { get; init; }
}

internal sealed record OpenRouterUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
