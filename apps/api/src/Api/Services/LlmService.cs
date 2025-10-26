using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Services;

/// <summary>
/// Service for LLM chat completions via OpenRouter API.
/// CONFIG-03: Supports dynamic configuration via database with fallback to hardcoded defaults.
/// </summary>
public class LlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LlmService> _logger;
    private readonly string _apiKey;
    private readonly IConfigurationService? _configService;
    private readonly IConfiguration? _fallbackConfig;

    // AI-15-ALT: Model selection configuration
    private readonly string _alternativeModelId;
    private readonly bool _useAlternativeModel;
    private readonly int _alternativeTrafficPercent;

    // Hardcoded defaults (CONFIG-03: Fallback when database not available)
    private const string DefaultChatModel = "deepseek/deepseek-chat-v3.1";
    private const string DefaultAlternativeModel = "openai/gpt-4o-mini";
    private const double DefaultTemperature = 0.3;
    private const int DefaultMaxTokens = 500;
    private const int DefaultTimeoutSeconds = 60;

    public LlmService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<LlmService> logger,
        IConfigurationService? configService = null,
        IConfiguration? fallbackConfig = null)
    {
        _httpClient = httpClientFactory.CreateClient("OpenRouter");
        _logger = logger;
        _apiKey = config["OPENROUTER_API_KEY"] ?? throw new InvalidOperationException("OPENROUTER_API_KEY not configured");
        _configService = configService;
        _fallbackConfig = fallbackConfig;

        // AI-15-ALT: Initialize model selection configuration
        _alternativeModelId = config["LlmService:AlternativeModelId"] ?? DefaultAlternativeModel;
        _useAlternativeModel = config.GetValue<bool>("LlmService:UseAlternativeModel");
        _alternativeTrafficPercent = config.GetValue<int>("LlmService:AlternativeModelTrafficPercentage");

        _logger.LogInformation(
            "LlmService initialized: AlternativeModel={AlternativeModel}, UseAlternative={UseAlternative}, TrafficPercent={TrafficPercent}%",
            _alternativeModelId, _useAlternativeModel, _alternativeTrafficPercent);

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://meepleai.app");

        // CONFIG-03: Use configurable timeout (will be set during first request if needed)
        // Default timeout here, can be overridden per request
        _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    }

    /// <summary>
    /// AI-15-ALT: Select model based on configuration and A/B testing logic.
    /// Returns configured model ID or alternative based on traffic percentage.
    /// </summary>
    private string SelectModel(string configuredModel)
    {
        // A/B testing: Random selection based on traffic percentage
        if (_alternativeTrafficPercent > 0)
        {
            var random = Random.Shared.Next(100);
            var selectedModel = random < _alternativeTrafficPercent ? _alternativeModelId : configuredModel;

            _logger.LogDebug(
                "A/B test model selection: random={Random}, threshold={Threshold}, selected={Selected}",
                random, _alternativeTrafficPercent, selectedModel);

            return selectedModel;
        }

        // Feature flag: Use alternative if enabled
        if (_useAlternativeModel)
        {
            _logger.LogDebug("Using alternative model via feature flag: {Model}", _alternativeModelId);
            return _alternativeModelId;
        }

        // Default: Use configured model
        return configuredModel;
    }

    /// <summary>
    /// Generate a chat completion response
    /// CONFIG-03: Uses dynamic configuration for model, temperature, and max_tokens.
    /// AI-15-ALT: Supports model selection with A/B testing.
    /// </summary>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return LlmCompletionResult.CreateFailure("No user prompt provided");
        }

        try
        {
            // CONFIG-03: Get configuration values with fallback chain
            var configuredModel = await GetAiConfigStringAsync("AI.Model", DefaultChatModel);
            // AI-15-ALT: Select model (configured or alternative based on A/B test)
            var model = SelectModel(configuredModel);
            var temperature = await GetAiConfigAsync("AI.Temperature", DefaultTemperature);
            var maxTokens = await GetAiConfigAsync("AI.MaxTokens", DefaultMaxTokens);

            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            messages.Add(new { role = "user", content = userPrompt });

            var request = new
            {
                model = model,
                messages = messages,
                temperature = temperature,
                max_tokens = maxTokens
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Generating chat completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
                model, temperature, maxTokens);

            var response = await _httpClient.PostAsync("chat/completions", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenRouter chat API error: {Status} - {Body}", response.StatusCode, responseBody);
                return LlmCompletionResult.CreateFailure($"API error: {response.StatusCode}");
            }

            var chatResponse = JsonSerializer.Deserialize<OpenRouterChatResponse>(responseBody);

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                return LlmCompletionResult.CreateFailure("No response returned from API");
            }

            var assistantMessage = chatResponse.Choices[0].Message?.Content ?? string.Empty;

            var usage = chatResponse.Usage != null
                ? new LlmUsage(
                    chatResponse.Usage.PromptTokens,
                    chatResponse.Usage.CompletionTokens,
                    chatResponse.Usage.TotalTokens)
                : LlmUsage.Empty;

            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(chatResponse.Id))
            {
                metadata["response_id"] = chatResponse.Id;
            }

            if (!string.IsNullOrWhiteSpace(chatResponse.Model))
            {
                metadata["model"] = chatResponse.Model;
            }

            var finishReason = chatResponse.Choices[0].FinishReason;
            if (!string.IsNullOrWhiteSpace(finishReason))
            {
                metadata["finish_reason"] = finishReason;
            }

            _logger.LogInformation("Successfully generated chat completion");

            return LlmCompletionResult.CreateSuccess(assistantMessage, usage, metadata);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Chat completion timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate chat completion");
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// CHAT-01: Generate a streaming chat completion response with token-by-token delivery via SSE
    /// CONFIG-03: Uses dynamic configuration for model, temperature, and max_tokens.
    /// AI-15-ALT: Supports model selection with A/B testing.
    /// </summary>
    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("Empty user prompt provided for streaming completion");
            yield break;
        }

        // CONFIG-03: Get configuration values with fallback chain
        var configuredModel = await GetAiConfigStringAsync("AI.Model", DefaultChatModel);
        // AI-15-ALT: Select model (configured or alternative based on A/B test)
        var model = SelectModel(configuredModel);
        var temperature = await GetAiConfigAsync("AI.Temperature", DefaultTemperature);
        var maxTokens = await GetAiConfigAsync("AI.MaxTokens", DefaultMaxTokens);

        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }

        messages.Add(new { role = "user", content = userPrompt });

        var request = new
        {
            model = model,
            messages = messages,
            temperature = temperature,
            max_tokens = maxTokens,
            stream = true // Enable streaming
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Starting streaming chat completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
            model, temperature, maxTokens);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = content
        };

        HttpResponseMessage? response = null;

        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OpenRouter streaming API error: {Status} - {Body}", response.StatusCode, errorBody);
                yield break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating streaming chat completion");
            response?.Dispose();
            yield break;
        }

        // Process stream without try-catch to allow yield return
        using (response)
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // SSE format: "data: {json}"
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6).Trim();

                    // OpenRouter sends "[DONE]" when stream is complete
                    if (data == "[DONE]")
                    {
                        _logger.LogInformation("Streaming completion finished");
                        break;
                    }

                    OpenRouterStreamChunk? chunk = null;
                    try
                    {
                        chunk = JsonSerializer.Deserialize<OpenRouterStreamChunk>(data);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse streaming chunk: {Data}", data);
                        continue;
                    }

                    var delta = chunk?.Choices?[0]?.Delta?.Content;
                    if (!string.IsNullOrEmpty(delta))
                    {
                        yield return delta;
                    }
                }
            }
        }
    }

    /// <summary>
    /// CHAT-02: Generate a JSON-structured response from the LLM, deserializing to the specified type.
    /// </summary>
    public async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default) where T : class
    {
        // Enhance system prompt with JSON instructions
        var enhancedSystemPrompt = $"""
            {systemPrompt}

            CRITICAL: You must return ONLY valid JSON. No markdown code blocks, no explanations, no additional text.
            Just the raw JSON object that matches the required structure.
            """;

        LlmCompletionResult? result = null;

        try
        {
            result = await GenerateCompletionAsync(enhancedSystemPrompt, userPrompt, ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
            {
                _logger.LogWarning("LLM completion failed or returned empty response for JSON generation");
                return null;
            }

            // Clean common LLM formatting artifacts
            var jsonText = CleanJsonResponse(result.Response);

            // Parse with Web-friendly JSON options (camelCase, tolerant)
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var parsed = JsonSerializer.Deserialize<T>(jsonText, options);

            if (parsed == null)
            {
                _logger.LogWarning("LLM returned valid JSON but deserialization produced null");
            }

            return parsed;
        }
        catch (JsonException ex)
        {
            var truncatedResponse = result?.Response?.Length > 500
                ? result.Response.Substring(0, 500) + "..."
                : result?.Response ?? "";
            _logger.LogWarning(ex,
                "Failed to parse LLM JSON response. Raw response: {Response}",
                truncatedResponse);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GenerateJsonAsync");
            return null;
        }
    }

    /// <summary>
    /// CONFIG-03: Get AI configuration value with fallback chain.
    /// Fallback chain: DB → appsettings.json → hardcoded defaults.
    /// </summary>
    private async Task<T> GetAiConfigAsync<T>(string configKey, T defaultValue) where T : struct
    {
        // 1. Try database configuration (highest priority)
        if (_configService != null)
        {
            var dbValue = await _configService.GetValueAsync<T?>(configKey);
            if (dbValue.HasValue)
            {
                var validated = ValidateAiConfig(dbValue.Value, configKey);
                _logger.LogDebug("AI config {Key}: {Value} (from database)", configKey, validated);
                return validated;
            }
        }

        // 2. Try appsettings.json (backward compatibility)
        if (_fallbackConfig != null)
        {
            var configPath = configKey.Replace(".", ":");
            var appsettingsValue = _fallbackConfig.GetValue<T?>(configPath);
            if (appsettingsValue.HasValue)
            {
                var validated = ValidateAiConfig(appsettingsValue.Value, configKey);
                _logger.LogDebug("AI config {Key}: {Value} (from appsettings)", configKey, validated);
                return validated;
            }
        }

        // 3. Hardcoded defaults (lowest priority)
        _logger.LogDebug("AI config {Key}: {Value} (using hardcoded default)", configKey, defaultValue);
        return defaultValue;
    }

    /// <summary>
    /// CONFIG-03: Get AI configuration value (string) with fallback chain.
    /// </summary>
    private async Task<string> GetAiConfigStringAsync(string configKey, string defaultValue)
    {
        // 1. Try database configuration
        if (_configService != null)
        {
            var dbValue = await _configService.GetValueAsync<string>(configKey);
            if (!string.IsNullOrWhiteSpace(dbValue))
            {
                _logger.LogDebug("AI config {Key}: {Value} (from database)", configKey, dbValue);
                return dbValue;
            }
        }

        // 2. Try appsettings.json
        if (_fallbackConfig != null)
        {
            var configPath = configKey.Replace(".", ":");
            var appsettingsValue = _fallbackConfig.GetValue<string>(configPath);
            if (!string.IsNullOrWhiteSpace(appsettingsValue))
            {
                _logger.LogDebug("AI config {Key}: {Value} (from appsettings)", configKey, appsettingsValue);
                return appsettingsValue;
            }
        }

        // 3. Hardcoded default
        _logger.LogDebug("AI config {Key}: {Value} (using hardcoded default)", configKey, defaultValue);
        return defaultValue;
    }

    /// <summary>
    /// CONFIG-03: Validate AI configuration values to ensure they are within acceptable bounds.
    /// </summary>
    private T ValidateAiConfig<T>(T value, string configKey) where T : struct
    {
        // Validate temperature (must be between 0.0 and 2.0 for most LLM APIs)
        if (configKey == "AI.Temperature" && value is double temperature)
        {
            if (temperature < 0.0 || temperature > 2.0)
            {
                _logger.LogWarning(
                    "AI temperature {Value} out of range [0.0, 2.0], using default {Default}",
                    temperature,
                    DefaultTemperature);
                return (T)(object)DefaultTemperature;
            }
        }

        // Validate max_tokens (must be positive, cap at reasonable upper bound)
        if (configKey == "AI.MaxTokens" && value is int maxTokens)
        {
            if (maxTokens <= 0)
            {
                _logger.LogWarning(
                    "AI max_tokens {Value} must be positive, using default {Default}",
                    maxTokens,
                    DefaultMaxTokens);
                return (T)(object)DefaultMaxTokens;
            }

            const int maxTokensUpperBound = 32000; // Reasonable upper bound for most models
            if (maxTokens > maxTokensUpperBound)
            {
                _logger.LogWarning(
                    "AI max_tokens {Value} exceeds maximum {MaxBound}, capping",
                    maxTokens,
                    maxTokensUpperBound);
                return (T)(object)maxTokensUpperBound;
            }
        }

        // Validate timeout (must be positive, cap at reasonable upper bound)
        if (configKey == "AI.TimeoutSeconds" && value is int timeoutSeconds)
        {
            if (timeoutSeconds <= 0)
            {
                _logger.LogWarning(
                    "AI timeout {Value} must be positive, using default {Default}",
                    timeoutSeconds,
                    DefaultTimeoutSeconds);
                return (T)(object)DefaultTimeoutSeconds;
            }

            const int maxTimeoutSeconds = 300; // 5 minutes max
            if (timeoutSeconds > maxTimeoutSeconds)
            {
                _logger.LogWarning(
                    "AI timeout {Value} exceeds maximum {MaxBound}, capping",
                    timeoutSeconds,
                    maxTimeoutSeconds);
                return (T)(object)maxTimeoutSeconds;
            }
        }

        return value;
    }

    /// <summary>
    /// Clean LLM response to extract pure JSON (remove markdown code blocks, etc.)
    /// </summary>
    private static string CleanJsonResponse(string response)
    {
        var cleaned = response.Trim();

        // Remove markdown code blocks (```json ... ``` or ``` ... ```)
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastBackticks = cleaned.LastIndexOf("```");

            if (firstNewline > 0 && lastBackticks > firstNewline)
            {
                cleaned = cleaned.Substring(firstNewline + 1, lastBackticks - firstNewline - 1).Trim();
            }
        }

        return cleaned;
    }
}

// OpenRouter API response models for chat completion
internal record OpenRouterChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; init; } = new();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("usage")]
    public ChatUsage? Usage { get; init; }
}

internal record ChatChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; init; } = string.Empty;
}

internal record ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

internal record ChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}

// CHAT-01: Streaming response models
internal record OpenRouterStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<StreamChoice>? Choices { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;
}

internal record StreamChoice
{
    [JsonPropertyName("delta")]
    public StreamDelta? Delta { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("index")]
    public int Index { get; init; }
}

internal record StreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
