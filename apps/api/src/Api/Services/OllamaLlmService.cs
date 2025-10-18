using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Services;

/// <summary>
/// Service for LLM chat completions via Ollama API (local, no API key needed)
/// </summary>
public class OllamaLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLlmService> _logger;
    private readonly string _ollamaUrl;
    private const string ChatModel = "llama3.2:3b";

    public OllamaLlmService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<OllamaLlmService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _logger = logger;
        _ollamaUrl = config["OLLAMA_URL"] ?? "http://ollama:11434";

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_ollamaUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(120); // Longer timeout for local models
    }

    /// <summary>
    /// Generate a chat completion response using Ollama
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
            var messages = new List<OllamaMessage>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new OllamaMessage { Role = "system", Content = systemPrompt });
            }

            messages.Add(new OllamaMessage { Role = "user", Content = userPrompt });

            var request = new OllamaChatRequest
            {
                Model = ChatModel,
                Messages = messages,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.3f, // Lower temperature for more deterministic, factual responses
                    NumPredict = 500    // Max tokens to generate
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Generating chat completion using Ollama {Model}", ChatModel);

            var response = await _httpClient.PostAsync("/api/chat", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama chat API error: {Status} - {Body}", response.StatusCode, responseBody);
                return LlmCompletionResult.CreateFailure($"API error: {response.StatusCode}");
            }

            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody);

            if (chatResponse?.Message == null || string.IsNullOrWhiteSpace(chatResponse.Message.Content))
            {
                return LlmCompletionResult.CreateFailure("No response returned from Ollama");
            }

            var assistantMessage = chatResponse.Message.Content;

            // Ollama doesn't always return precise token counts, estimate if needed
            var promptTokens = chatResponse.PromptEvalCount > 0 ? chatResponse.PromptEvalCount : EstimateTokens(systemPrompt + userPrompt);
            var completionTokens = chatResponse.EvalCount > 0 ? chatResponse.EvalCount : EstimateTokens(assistantMessage);
            var usage = new LlmUsage(promptTokens, completionTokens, promptTokens + completionTokens);

            var metadata = new Dictionary<string, string>
            {
                ["model"] = chatResponse.Model ?? ChatModel,
                ["done_reason"] = chatResponse.DoneReason ?? "stop"
            };

            if (chatResponse.TotalDuration > 0)
            {
                metadata["total_duration_ms"] = (chatResponse.TotalDuration / 1_000_000).ToString();
            }

            _logger.LogInformation("Successfully generated chat completion using Ollama");

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

    private static int EstimateTokens(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        return string.IsNullOrWhiteSpace(text) ? 0 : text.Length / 4;
    }

    /// <summary>
    /// CHAT-01: Generate a streaming chat completion response with token-by-token delivery
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

        var messages = new List<OllamaMessage>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new OllamaMessage { Role = "system", Content = systemPrompt });
        }

        messages.Add(new OllamaMessage { Role = "user", Content = userPrompt });

        var request = new OllamaChatRequest
        {
            Model = ChatModel,
            Messages = messages,
            Stream = true, // Enable streaming
            Options = new OllamaOptions
            {
                Temperature = 0.3f,
                NumPredict = 500
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Starting streaming chat completion using Ollama {Model}", ChatModel);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
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
                _logger.LogError("Ollama streaming API error: {Status} - {Body}", response.StatusCode, errorBody);
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

                OllamaChatResponse? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse streaming chunk: {Line}", line);
                    continue;
                }

                var delta = chunk?.Message?.Content;
                if (!string.IsNullOrEmpty(delta))
                {
                    yield return delta;
                }

                if (chunk?.Done == true)
                {
                    _logger.LogInformation("Streaming completion finished");
                    break;
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

// Ollama API request/response models
internal record OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaMessage> Messages { get; init; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; init; }
}

internal record OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

internal record OllamaOptions
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; init; }

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; init; }
}

internal record OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }

    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; init; }

    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; init; }

    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; init; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; init; }
}
