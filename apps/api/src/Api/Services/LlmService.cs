using System.Collections.Generic;
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
    private const string ChatModel = "anthropic/claude-3.5-sonnet";

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
