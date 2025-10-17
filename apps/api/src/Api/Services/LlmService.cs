using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Services;

/// <summary>
/// Service for LLM chat completions via OpenRouter API
/// </summary>
public class LlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LlmService> _logger;
    private readonly string _apiKey;
    private const string ChatModel = "deepseek/deepseek-chat-v3.1";

    public LlmService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<LlmService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenRouter");
        _logger = logger;
        _apiKey = config["OPENROUTER_API_KEY"] ?? throw new InvalidOperationException("OPENROUTER_API_KEY not configured");

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://meepleai.app");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Generate a chat completion response
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
            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            messages.Add(new { role = "user", content = userPrompt });

            var request = new
            {
                model = ChatModel,
                messages = messages,
                temperature = 0.3, // Lower temperature for more deterministic, factual responses
                max_tokens = 500
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Generating chat completion using {Model}", ChatModel);

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

        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }

        messages.Add(new { role = "user", content = userPrompt });

        var request = new
        {
            model = ChatModel,
            messages = messages,
            temperature = 0.3,
            max_tokens = 500,
            stream = true // Enable streaming
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Starting streaming chat completion using {Model}", ChatModel);

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
