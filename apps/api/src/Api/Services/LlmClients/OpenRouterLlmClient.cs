using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Api.Models;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Services.LlmClients;

/// <summary>
/// LLM client for OpenRouter API (https://openrouter.ai)
/// ISSUE-958: Hybrid LLM architecture - OpenRouter provider for high-quality inference
/// </summary>
/// <remarks>
/// OpenRouter provides access to multiple LLM providers (OpenAI, Anthropic, Google, etc.)
/// through a unified API. Used for authenticated/premium users requiring high-quality responses.
/// Models: openai/gpt-4o-mini, anthropic/claude-3.5-sonnet, deepseek/deepseek-chat-v3.1, etc.
/// </remarks>
public class OpenRouterLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterLlmClient> _logger;
    private readonly ILlmCostCalculator _costCalculator;
    private readonly string _apiKey;

    // Hardcoded defaults
    private const int DefaultTimeoutSeconds = 60;

    public string ProviderName => "OpenRouter";

    public OpenRouterLlmClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILlmCostCalculator costCalculator,
        ILogger<OpenRouterLlmClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenRouter");
        _logger = logger;
        _costCalculator = costCalculator;

        // SEC-708: Read API key from Docker Secret file or direct config
        _apiKey = SecretsHelper.GetSecretOrValue(config, "OPENROUTER_API_KEY", logger, required: true)
            ?? throw new InvalidOperationException("OPENROUTER_API_KEY not configured");

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://meepleai.app");
        _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

        _logger.LogInformation("OpenRouterLlmClient initialized with cost tracking");
    }

    /// <inheritdoc/>
    public bool SupportsModel(string modelId)
    {
        // OpenRouter models have provider prefix (e.g., "openai/gpt-4o-mini", "anthropic/claude-3.5-sonnet")
        return modelId.Contains('/');
    }

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
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
                model = model,
                messages = messages,
                temperature = temperature,
                max_tokens = maxTokens
            };

            var json = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            _logger.LogInformation("Generating OpenRouter completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
                model, temperature, maxTokens);

            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenRouter API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(responseBody));
                    var statusCode = (int)response.StatusCode;
                    return LlmCompletionResult.CreateFailure($"OpenRouter API error: {statusCode} ({response.StatusCode})");
                }

                var chatResponse = JsonSerializer.Deserialize<OpenRouterChatResponse>(responseBody);

                if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
                {
                    return LlmCompletionResult.CreateFailure("No response returned from OpenRouter");
                }

                var assistantMessage = chatResponse.Choices[0].Message?.Content ?? string.Empty;

                var usage = chatResponse.Usage != null
                    ? new LlmUsage(
                        chatResponse.Usage.PromptTokens,
                        chatResponse.Usage.CompletionTokens,
                        chatResponse.Usage.TotalTokens)
                    : LlmUsage.Empty;

                // ISSUE-960: Calculate cost for this request
                var costCalculation = _costCalculator.CalculateCost(
                    model,
                    ProviderName,
                    usage.PromptTokens,
                    usage.CompletionTokens);

                var cost = new LlmCost
                {
                    InputCost = costCalculation.InputCost,
                    OutputCost = costCalculation.OutputCost,
                    ModelId = model,
                    Provider = ProviderName
                };

                var metadata = new Dictionary<string, string>
(StringComparer.Ordinal)
                {
                    ["provider"] = "OpenRouter",
                    ["cost_usd"] = cost.TotalCost.ToString("F6", CultureInfo.InvariantCulture)
                };

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

                _logger.LogInformation("Successfully generated OpenRouter completion (cost: ${Cost:F6})", cost.TotalCost);

                return LlmCompletionResult.CreateSuccess(assistantMessage, usage, cost, metadata);
            }
            finally
            {
                response?.Dispose();
                httpRequest.Dispose();
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "OpenRouter completion timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during OpenRouter completion");
            return LlmCompletionResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize OpenRouter response");
            return LlmCompletionResult.CreateFailure("Invalid response format");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during OpenRouter completion");
            return LlmCompletionResult.CreateFailure($"Configuration error: {ex.Message}");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary using Result pattern - must return failure instead of throwing
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OpenRouter completion");
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("Empty user prompt provided for OpenRouter streaming");
            yield break;
        }

        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }

        messages.Add(new { role = "user", content = userPrompt });

        // ISSUE-1725: Enable usage metadata in SSE stream
        var request = new
        {
            model = model,
            messages = messages,
            temperature = temperature,
            max_tokens = maxTokens,
            stream = true,
            usage = new { include = true } // Request usage metadata in final chunk
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Starting OpenRouter streaming completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
            model, temperature, maxTokens);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = content
        };

        HttpResponseMessage? response = null;

        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError("OpenRouter streaming API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(errorBody));
                response.Dispose();
                yield break;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed initiating OpenRouter streaming");
            response?.Dispose();
            yield break;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "OpenRouter streaming request timed out");
            response?.Dispose();
            yield break;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation initiating OpenRouter streaming");
            response?.Dispose();
            yield break;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Streaming generator boundary - must handle all errors gracefully
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initiating OpenRouter streaming");
            response?.Dispose();
            yield break;
        }
#pragma warning restore CA1031

        // Process stream
        using (response)
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // SSE format: "data: {json}"
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6).Trim();

                    // OpenRouter sends "[DONE]" when stream is complete
                    if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
                    {
                        _logger.LogInformation("OpenRouter streaming finished");
                        break;
                    }

                    OpenRouterStreamChunk? chunk = null;
                    try
                    {
                        chunk = JsonSerializer.Deserialize<OpenRouterStreamChunk>(data);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse OpenRouter streaming chunk: {Data}", data);
                        continue;
                    }

                    // Regular content chunk (may be present even with usage)
                    var delta = chunk?.Choices?[0]?.Delta?.Content;
                    var hasContent = !string.IsNullOrEmpty(delta);

                    // ISSUE-1725: Check for usage metadata in chunk (final chunk)
                    var hasUsage = chunk?.Usage != null;

                    if (hasContent && !hasUsage)
                    {
                        // Pure content chunk
                        yield return new StreamChunk(Content: delta);
                    }
                    else if (hasUsage)
                    {
                        // Final chunk with usage metadata (may also have content)
                        if (hasContent)
                        {
                            // Yield content first
                            yield return new StreamChunk(Content: delta);
                        }

                        // Then yield usage metadata
                        var usage = new LlmUsage(
                            chunk!.Usage!.PromptTokens,
                            chunk.Usage.CompletionTokens,
                            chunk.Usage.TotalTokens);

                        var cost = _costCalculator.CalculateCost(
                            chunk.Model,
                            ProviderName,
                            usage.PromptTokens,
                            usage.CompletionTokens);

                        var llmCost = new LlmCost
                        {
                            InputCost = cost.InputCost,
                            OutputCost = cost.OutputCost,
                            ModelId = chunk.Model,
                            Provider = ProviderName
                        };

                        _logger.LogInformation(
                            "OpenRouter streaming usage: {PromptTokens}p + {CompletionTokens}c = {TotalTokens}t (${TotalCost:F6})",
                            usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens, llmCost.TotalCost);

                        yield return new StreamChunk(
                            Content: null,
                            Usage: usage,
                            Cost: llmCost,
                            IsFinal: true);
                        break; // Usage signals end of stream
                    }
                }
            }
        }
    }
}

// OpenRouter API response models (reused from existing LlmService.cs)
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

// Streaming response models
internal record OpenRouterStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<StreamChoice>? Choices { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// ISSUE-1725: Usage metadata in final SSE chunk (when usage.include=true)
    /// </summary>
    [JsonPropertyName("usage")]
    public ChatUsage? Usage { get; init; }
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