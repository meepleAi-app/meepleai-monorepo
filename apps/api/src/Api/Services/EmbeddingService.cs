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
    private readonly HttpClient _localEmbeddingClient; // AI-09: Local embedding service
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string? _apiKey;
    private readonly string _embeddingModel;
    private readonly string _provider;
    private readonly string? _localEmbeddingUrl; // AI-09: Local embedding service URL
    private readonly bool _embeddingFallbackEnabled; // AI-09: Enable fallback chain
    private readonly int _embeddingDimensions; // Configured based on model

    public EmbeddingService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _logger = logger;

        // AI-09: Local embedding service configuration
        _localEmbeddingUrl = config["LOCAL_EMBEDDING_URL"];
        _embeddingFallbackEnabled = bool.TryParse(config["EMBEDDING_FALLBACK_ENABLED"], out var fallback) && fallback;

        // Create HTTP client for local embedding service
        if (!string.IsNullOrWhiteSpace(_localEmbeddingUrl))
        {
            _localEmbeddingClient = httpClientFactory.CreateClient("LocalEmbedding");
            _localEmbeddingClient.BaseAddress = new Uri(_localEmbeddingUrl);
            _localEmbeddingClient.Timeout = TimeSpan.FromSeconds(30);
            _logger.LogInformation("Local embedding service configured at {Url}", _localEmbeddingUrl);
        }
        else
        {
            _localEmbeddingClient = httpClientFactory.CreateClient();
            _logger.LogInformation("Local embedding service not configured");
        }

        // Check for embedding provider configuration (default: ollama)
        // Support both nested (Embedding:Provider) and flat (EMBEDDING_PROVIDER) configuration keys
        _provider = config["Embedding:Provider"]?.ToLowerInvariant()
                    ?? config["EMBEDDING_PROVIDER"]?.ToLowerInvariant()
                    ?? "ollama";

        if (_provider == "ollama")
        {
            // Use Ollama for local embeddings (no API key needed)
            _httpClient = httpClientFactory.CreateClient("Ollama");
            var ollamaUrl = config["OLLAMA_URL"] ?? "http://localhost:11434";
            _httpClient.BaseAddress = new Uri(ollamaUrl);

            // Support both nested (Embedding:Model) and flat (EMBEDDING_MODEL) configuration keys
            _embeddingModel = config["Embedding:Model"] ?? config["EMBEDDING_MODEL"] ?? "nomic-embed-text";
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            // Determine dimensions based on model
            _embeddingDimensions = DetermineEmbeddingDimensions(_embeddingModel, config);

            _logger.LogInformation("Using Ollama for embeddings at {Url} with model {Model} ({Dimensions} dimensions)",
                ollamaUrl, _embeddingModel, _embeddingDimensions);
        }
        else if (_provider == "openai")
        {
            // Use OpenRouter API (OpenAI-compatible)
            _httpClient = httpClientFactory.CreateClient("OpenRouter");
            _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            _apiKey = config["OPENAI_API_KEY"] ?? throw new InvalidOperationException("OPENAI_API_KEY not configured for OpenAI provider");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            // Support both nested (Embedding:Model) and flat (EMBEDDING_MODEL) configuration keys
            _embeddingModel = config["Embedding:Model"] ?? config["EMBEDDING_MODEL"] ?? "text-embedding-3-small";
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Determine dimensions based on model
            _embeddingDimensions = DetermineEmbeddingDimensions(_embeddingModel, config);

            _logger.LogInformation("Using OpenRouter for embeddings with model {Model} ({Dimensions} dimensions)",
                _embeddingModel, _embeddingDimensions);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported embedding provider: {_provider}. Use 'ollama' or 'openai'");
        }
    }

    /// <summary>
    /// Get the configured embedding dimensions
    /// </summary>
    public int GetEmbeddingDimensions() => _embeddingDimensions;

    /// <summary>
    /// Determine embedding dimensions based on model name and configuration
    /// </summary>
    private static int DetermineEmbeddingDimensions(string modelName, IConfiguration config)
    {
        // Try nested config first (Embedding:Dimensions) - matches appsettings.json structure
        if (int.TryParse(config["Embedding:Dimensions"], out var nestedDimensions) && nestedDimensions > 0)
        {
            return nestedDimensions;
        }

        // Try flat config for backward compatibility (EMBEDDING_DIMENSIONS environment variable)
        // IMPORTANT: Only accept positive values to prevent 0 or negative dimensions bug
        if (int.TryParse(config["EMBEDDING_DIMENSIONS"], out var flatDimensions) && flatDimensions > 0)
        {
            return flatDimensions;
        }

        // Infer from model name as fallback
        return modelName.ToLowerInvariant() switch
        {
            // OpenAI models
            "text-embedding-ada-002" => 1536,
            "text-embedding-3-small" => 1536,
            "text-embedding-3-large" => 3072,
            // Ollama models
            "nomic-embed-text" => 768,
            "all-minilm" => 384,
            "mxbai-embed-large" => 1024,
            // Sentence transformers
            "all-minilm-l6-v2" => 384,
            "all-mpnet-base-v2" => 768,
            // Default to 768 for unknown models (Ollama default)
            _ => 768
        };
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
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during embedding generation");
            return EmbeddingResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize embedding response");
            return EmbeddingResult.CreateFailure("Invalid response format");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during embedding generation");
            return EmbeddingResult.CreateFailure($"Configuration error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during embedding generation");
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
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

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
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

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

    // AI-09: Multi-language embedding support with fallback chain

    /// <summary>
    /// Generate embeddings for texts with language-specific model selection and fallback chain
    /// Fallback order: Local → Ollama → OpenRouter
    /// </summary>
    public async Task<EmbeddingResult> GenerateEmbeddingsAsync(
        List<string> texts,
        string language,
        CancellationToken ct = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return EmbeddingResult.CreateFailure("No texts provided");
        }

        // Validate language code
        if (!IsValidLanguage(language))
        {
            _logger.LogWarning("Unsupported language code: {Language}, falling back to 'en'", language);
            language = "en";
        }

        try
        {
            // 1. Try local embedding service first (if configured and enabled)
            if (_embeddingFallbackEnabled && !string.IsNullOrWhiteSpace(_localEmbeddingUrl))
            {
                var localResult = await TryLocalEmbeddingAsync(texts, language, ct);
                if (localResult.Success)
                {
                    _logger.LogInformation("Successfully generated embeddings using local service for language {Language}", language);
                    return localResult;
                }

                _logger.LogWarning("Local embedding service failed, falling back to {Provider}", _provider);
            }

            // 2. Fall back to configured provider (Ollama or OpenRouter)
            if (_provider == "ollama")
            {
                return await GenerateOllamaEmbeddingsAsync(texts, ct);
            }
            else // openai/openrouter
            {
                return await GenerateOpenRouterEmbeddingAsync(texts, language, ct);
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Embedding generation timed out for language {Language}", language);
            return EmbeddingResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during embedding generation for language {Language}", language);
            return EmbeddingResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize embedding response for language {Language}", language);
            return EmbeddingResult.CreateFailure("Invalid response format");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during embedding generation for language {Language}", language);
            return EmbeddingResult.CreateFailure($"Configuration error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during embedding generation for language {Language}", language);
            return EmbeddingResult.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate embedding for a single text with language-specific model
    /// </summary>
    public async Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string language,
        CancellationToken ct = default)
    {
        return await GenerateEmbeddingsAsync(new List<string> { text }, language, ct);
    }

    /// <summary>
    /// Try to generate embeddings using local Python service
    /// </summary>
    private async Task<EmbeddingResult> TryLocalEmbeddingAsync(
        List<string> texts,
        string language,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Attempting local embedding service for {Count} texts in language {Language}", texts.Count, language);

            var request = new LocalEmbeddingRequest
            {
                Texts = texts,
                Language = language
            };

            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _localEmbeddingClient.PostAsync("/embeddings", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Local embedding service returned error: {Status} - {Body}", response.StatusCode, responseBody);
                return EmbeddingResult.CreateFailure($"Local service error: {(int)response.StatusCode}");
            }

            var embeddingResponse = JsonSerializer.Deserialize<LocalEmbeddingResponse>(responseBody);

            if (embeddingResponse?.Embeddings == null || embeddingResponse.Embeddings.Count == 0)
            {
                return EmbeddingResult.CreateFailure("No embeddings returned from local service");
            }

            _logger.LogInformation("Successfully generated {Count} embeddings via local service (dimension: {Dim})",
                embeddingResponse.Count, embeddingResponse.Dimension);

            return EmbeddingResult.CreateSuccess(embeddingResponse.Embeddings);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Local embedding service unavailable");
            return EmbeddingResult.CreateFailure("Local service unavailable");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Local embedding service request timed out");
            return EmbeddingResult.CreateFailure("Local service timeout");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize local embedding service response");
            return EmbeddingResult.CreateFailure("Invalid local service response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling local embedding service");
            return EmbeddingResult.CreateFailure($"Local service error: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate embeddings via OpenRouter with language-specific model selection
    /// </summary>
    private async Task<EmbeddingResult> GenerateOpenRouterEmbeddingAsync(
        List<string> texts,
        string language,
        CancellationToken ct)
    {
        // For now, use the same model for all languages
        // Future: AI-09.2 will add language-specific model selection via config
        _logger.LogInformation("Generating embeddings for {Count} texts in language {Language} using OpenRouter model {Model}",
            texts.Count, language, _embeddingModel);

        return await GenerateOpenAIEmbeddingsAsync(texts, ct);
    }

    /// <summary>
    /// Validate language code
    /// </summary>
    private static bool IsValidLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return false;

        var supportedLanguages = new[] { "en", "it", "de", "fr", "es" };
        return supportedLanguages.Contains(languageCode.ToLowerInvariant());
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

// AI-09: Local embedding service request/response models
internal record LocalEmbeddingRequest
{
    [JsonPropertyName("texts")]
    public List<string> Texts { get; init; } = new();

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";
}

internal record LocalEmbeddingResponse
{
    [JsonPropertyName("embeddings")]
    public List<float[]> Embeddings { get; init; } = new();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("dimension")]
    public int Dimension { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }
}
