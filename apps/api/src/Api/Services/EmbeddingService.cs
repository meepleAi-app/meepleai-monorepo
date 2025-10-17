using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Services;

/// <summary>
/// Service for generating text embeddings via Ollama (local) or OpenAI API
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string? _apiKey;
    private readonly string _embeddingModel;
    private readonly string _provider;
    private const int EmbeddingDimensions = 768; // nomic-embed-text default

    public EmbeddingService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _logger = logger;

        // Check for embedding provider configuration (default: ollama)
        _provider = config["EMBEDDING_PROVIDER"]?.ToLowerInvariant() ?? "ollama";

        if (_provider == "ollama")
        {
            // Use Ollama for local embeddings (no API key needed)
            _httpClient = httpClientFactory.CreateClient("Ollama");
            var ollamaUrl = config["OLLAMA_URL"] ?? "http://localhost:11434";
            _httpClient.BaseAddress = new Uri(ollamaUrl);
            _embeddingModel = config["EMBEDDING_MODEL"] ?? "nomic-embed-text";
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _logger.LogInformation("Using Ollama for embeddings at {Url} with model {Model}", ollamaUrl, _embeddingModel);
        }
        else if (_provider == "openai")
        {
            // Use OpenRouter API (OpenAI-compatible)
            _httpClient = httpClientFactory.CreateClient("OpenRouter");
            _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            _apiKey = config["OPENAI_API_KEY"] ?? throw new InvalidOperationException("OPENAI_API_KEY not configured for OpenAI provider");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _embeddingModel = config["EMBEDDING_MODEL"] ?? "text-embedding-3-small";
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _logger.LogInformation("Using OpenRouter for embeddings with model {Model}", _embeddingModel);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported embedding provider: {_provider}. Use 'ollama' or 'openai'");
        }
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
            if (_provider == "ollama")
            {
                return await GenerateOllamaEmbeddingsAsync(texts, ct);
            }
            else // openai
            {
                return await GenerateOpenAIEmbeddingsAsync(texts, ct);
            }
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

    private async Task<EmbeddingResult> GenerateOllamaEmbeddingsAsync(List<string> texts, CancellationToken ct)
    {
        _logger.LogInformation("Generating embeddings for {Count} texts using Ollama model {Model}", texts.Count, _embeddingModel);

        var embeddings = new List<float[]>();

        // Ollama /api/embeddings endpoint processes one text at a time
        foreach (var text in texts)
        {
            var request = new
            {
                model = _embeddingModel,
                prompt = text
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/embeddings", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama embeddings API error: {Status} - {Body}", response.StatusCode, responseBody);
                return EmbeddingResult.CreateFailure($"API error: {(int)response.StatusCode}");
            }

            var ollamaResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseBody);

            if (ollamaResponse?.Embedding == null || ollamaResponse.Embedding.Length == 0)
            {
                return EmbeddingResult.CreateFailure("No embedding returned from Ollama");
            }

            embeddings.Add(ollamaResponse.Embedding);
        }

        _logger.LogInformation("Successfully generated {Count} embeddings via Ollama", embeddings.Count);
        return EmbeddingResult.CreateSuccess(embeddings);
    }

    private async Task<EmbeddingResult> GenerateOpenAIEmbeddingsAsync(List<string> texts, CancellationToken ct)
    {
        _logger.LogInformation("Generating embeddings for {Count} texts using OpenAI model {Model}", texts.Count, _embeddingModel);

        var request = new
        {
            model = _embeddingModel,
            input = texts,
            encoding_format = "float"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("embeddings", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI embeddings API error: {Status} - {Body}", response.StatusCode, responseBody);
            return EmbeddingResult.CreateFailure($"API error: {(int)response.StatusCode}");
        }

        var embeddingResponse = JsonSerializer.Deserialize<OpenAIEmbeddingResponse>(responseBody);

        if (embeddingResponse?.Data == null || embeddingResponse.Data.Count == 0)
        {
            return EmbeddingResult.CreateFailure("No embeddings returned from OpenAI");
        }

        var embeddings = embeddingResponse.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();

        _logger.LogInformation("Successfully generated {Count} embeddings via OpenAI", embeddings.Count);
        return EmbeddingResult.CreateSuccess(embeddings);
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

// Ollama API response model
internal record OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = Array.Empty<float>();
}

// OpenAI API response models
internal record OpenAIEmbeddingResponse
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
