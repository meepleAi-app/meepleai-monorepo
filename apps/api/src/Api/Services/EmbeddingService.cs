using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Services;

/// <summary>
/// Service for generating text embeddings via OpenRouter API
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _apiKey;
    private const string EmbeddingModel = "openai/text-embedding-3-small";
    private const int EmbeddingDimensions = 1536; // text-embedding-3-small default

    public EmbeddingService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenRouter");
        _logger = logger;
        _apiKey = config["OPENROUTER_API_KEY"] ?? throw new InvalidOperationException("OPENROUTER_API_KEY not configured");

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://meepleai.app");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Generate embeddings for a list of text chunks
    /// </summary>
    public async Task<EmbeddingResult> GenerateEmbeddingsAsync(
        List<string> texts,
        CancellationToken ct = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return EmbeddingResult.CreateFailure("No texts provided");
        }

        try
        {
            var request = new
            {
                model = EmbeddingModel,
                input = texts,
                encoding_format = "float"
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Generating embeddings for {Count} texts using {Model}", texts.Count, EmbeddingModel);

            var response = await _httpClient.PostAsync("embeddings", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenRouter embeddings API error: {Status} - {Body}", response.StatusCode, responseBody);
                return EmbeddingResult.CreateFailure($"API error: {response.StatusCode}");
            }

            var embeddingResponse = JsonSerializer.Deserialize<OpenRouterEmbeddingResponse>(responseBody);

            if (embeddingResponse?.Data == null || embeddingResponse.Data.Count == 0)
            {
                return EmbeddingResult.CreateFailure("No embeddings returned from API");
            }

            var embeddings = embeddingResponse.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToList();

            _logger.LogInformation("Successfully generated {Count} embeddings", embeddings.Count);

            return EmbeddingResult.CreateSuccess(embeddings);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Embedding generation timed out");
            return EmbeddingResult.CreateFailure("Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings");
            return EmbeddingResult.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate embedding for a single text
    /// </summary>
    public virtual async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var result = await GenerateEmbeddingsAsync(new List<string> { text }, ct);
        return result;
    }
}

/// <summary>
/// Result of embedding generation
/// </summary>
public record EmbeddingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<float[]> Embeddings { get; init; } = new();

    public static EmbeddingResult CreateSuccess(List<float[]> embeddings) =>
        new() { Success = true, Embeddings = embeddings };

    public static EmbeddingResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

// OpenRouter API response models
internal record OpenRouterEmbeddingResponse
{
    [JsonPropertyName("data")]
    public List<EmbeddingData> Data { get; init; } = new();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("usage")]
    public Usage? Usage { get; init; }
}

internal record EmbeddingData
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = Array.Empty<float>();

    [JsonPropertyName("index")]
    public int Index { get; init; }
}

internal record Usage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
