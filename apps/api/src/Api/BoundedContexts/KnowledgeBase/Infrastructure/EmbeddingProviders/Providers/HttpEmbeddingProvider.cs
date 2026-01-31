using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;

/// <summary>
/// HTTP-based embedding provider for external embedding service.
/// Calls Python microservice (embedding-service) to avoid OOM in API container.
/// Implements batch processing natively via HTTP calls.
/// </summary>
internal sealed class HttpEmbeddingProvider : EmbeddingProviderBase
{
    private readonly string _serviceUrl;
    private readonly string _modelName;
    private readonly int _dimensions;

    public HttpEmbeddingProvider(
        HttpClient httpClient,
        ILogger<HttpEmbeddingProvider> logger,
        EmbeddingConfiguration config)
        : base(httpClient, logger, config)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default fallback for external service URL
        _serviceUrl = config.LocalServiceUrl ?? "http://embedding-service:8000";
#pragma warning restore S1075
        _modelName = config.Model ?? "intfloat/multilingual-e5-large";
        _dimensions = config.Dimensions ?? 1024;

        // HttpClient configured in InfrastructureServiceExtensions
    }

    public override string ProviderName => "External";
    public override string ModelName => _modelName;
    public override int Dimensions => _dimensions;
    public override int MaxContextTokens => 8192;

    public override async Task<EmbeddingProviderResult> GenerateBatchEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return EmbeddingProviderResult.CreateFailure("No texts provided");
        }

        Logger.LogInformation(
            "Generating embeddings for {Count} texts using external service at {Url}",
            texts.Count, _serviceUrl);

        try
        {
            var request = new ExternalEmbeddingRequest
            {
                Texts = texts.ToList(),
                Language = "en" // Default to English (can be made configurable)
            };

            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Call external embedding service
            using var response = await HttpClient.PostAsync("/embeddings", content, cancellationToken)
                .ConfigureAwait(false);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("External embedding service error: {Status} - {Body}",
                    response.StatusCode, responseBody);
                return EmbeddingProviderResult.CreateFailure(
                    $"Service error: {(int)response.StatusCode}");
            }

            // Parse response
            var result = JsonSerializer.Deserialize<ExternalEmbeddingResponse>(
                responseBody, JsonOptions);

            if (result?.Embeddings == null || result.Embeddings.Count == 0)
            {
                return EmbeddingProviderResult.CreateFailure(
                    "No embeddings returned from external service");
            }

            // Validate dimensions
            if (result.Embeddings.Any(emb => emb.Length != _dimensions))
            {
                return EmbeddingProviderResult.CreateFailure(
                    $"Invalid embedding dimensions (expected {_dimensions})");
            }

            Logger.LogInformation("Successfully generated {Count} embeddings via external service",
                result.Embeddings.Count);

            return EmbeddingProviderResult.CreateSuccess(result.Embeddings, _modelName);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error calling external embedding service");
            return EmbeddingProviderResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            Logger.LogError(ex, "Timeout calling external embedding service");
            return EmbeddingProviderResult.CreateFailure("Request timeout");
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "JSON parsing error from external service");
            return EmbeddingProviderResult.CreateFailure($"Invalid response: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error calling external embedding service");
            return EmbeddingProviderResult.CreateFailure($"Unexpected error: {ex.Message}");
        }
    }

    public override async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var healthResponse = await HttpClient.GetAsync("/health", cancellationToken)
                .ConfigureAwait(false);
            return healthResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

// Request/Response models for external embedding service
internal sealed record ExternalEmbeddingRequest
{
    [JsonPropertyName("texts")]
    public List<string> Texts { get; init; } = new();

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";
}

internal sealed record ExternalEmbeddingResponse
{
    [JsonPropertyName("embeddings")]
    public List<float[]> Embeddings { get; init; } = new();

    [JsonPropertyName("model")]
    public string? Model { get; init; }
}
