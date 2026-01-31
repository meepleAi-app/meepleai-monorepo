using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Infrastructure.Security;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Services.LlmClients;

/// <summary>
/// LLM client for Ollama local models (default: http://ollama:11434)
/// ISSUE-958: Hybrid LLM architecture - Ollama provider for cost-effective inference
/// </summary>
/// <remarks>
/// Ollama provides OpenAI-compatible API format for local model inference.
/// Supports models like llama3:8b, llama3.1:8b, mistral, etc.
/// Used for anonymous/low-tier users or A/B testing traffic routing.
/// </remarks>
internal class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLlmClient> _logger;
    private readonly ILlmCostCalculator _costCalculator;

    // Hardcoded defaults for Ollama
    private const int DefaultTimeoutSeconds = 60;

    public string ProviderName => "Ollama";

    public OllamaLlmClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILlmCostCalculator costCalculator,
        ILogger<OllamaLlmClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _logger = logger;
        _costCalculator = costCalculator;

        // S1075: Default Ollama URL extracted to const
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        const string DefaultOllamaUrl = "http://ollama:11434";
#pragma warning restore S1075

        // Configure Ollama endpoint - check multiple config keys for flexibility
        // Docker uses OLLAMA_URL env var, appsettings.json may use OllamaUrl
        var ollamaUrl = config["OLLAMA_URL"] ?? config["OllamaUrl"] ?? DefaultOllamaUrl;
        _httpClient.BaseAddress = new Uri(ollamaUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

        _logger.LogInformation("OllamaLlmClient initialized with endpoint: {OllamaUrl} (zero cost - self-hosted)", ollamaUrl);
    }

    /// <inheritdoc/>
    public bool SupportsModel(string modelId)
    {
        // Ollama models don't have provider prefix (e.g., "llama3:8b", not "ollama/llama3:8b")
        // Check if model ID doesn't contain provider prefix
        return !modelId.Contains('/');
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
            using var httpRequest = CreateChatRequest(model, systemPrompt, userPrompt, temperature, maxTokens, stream: false);

            _logger.LogInformation("Generating Ollama completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
                model, temperature, maxTokens);

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            return await HandleCompletionResponseAsync(response, model, systemPrompt, userPrompt, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Ollama completion timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during Ollama completion");
            return LlmCompletionResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Ollama response");
            return LlmCompletionResult.CreateFailure("Invalid response format");
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Wraps unexpected Ollama API errors into domain-friendly LlmCompletionResult
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Ollama completion");
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    private HttpRequestMessage CreateChatRequest(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
        bool stream)
    {
        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }

        messages.Add(new { role = "user", content = userPrompt });

        var requestPayload = new
        {
            model = model,
            messages = messages,
            options = new
            {
                temperature = temperature,
                num_predict = maxTokens
            },
            stream = stream
        };

        var json = JsonSerializer.Serialize(requestPayload);
        return new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private async Task<LlmCompletionResult> HandleCompletionResponseAsync(
        HttpResponseMessage response,
        string model,
        string systemPrompt,
        string userPrompt,
        CancellationToken ct)
    {
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Ollama API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(responseBody));
            return LlmCompletionResult.CreateFailure($"Ollama API error: {(int)response.StatusCode} ({response.StatusCode})");
        }

        var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody);

        if (chatResponse?.Message == null)
        {
            return LlmCompletionResult.CreateFailure("No response returned from Ollama");
        }

        var assistantMessage = chatResponse.Message.Content ?? string.Empty;
        var estimatedPromptTokens = EstimateTokenCount(systemPrompt + userPrompt);
        var estimatedCompletionTokens = EstimateTokenCount(assistantMessage);

        var usage = new LlmUsage(
            estimatedPromptTokens,
            estimatedCompletionTokens,
            estimatedPromptTokens + estimatedCompletionTokens);

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

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model"] = chatResponse.Model ?? model,
            ["provider"] = "Ollama",
            ["cost_usd"] = "0.000000"
        };

        if (!string.IsNullOrWhiteSpace(chatResponse.DoneReason))
        {
            metadata["finish_reason"] = chatResponse.DoneReason;
        }

        _logger.LogInformation("Successfully generated Ollama completion (cost: $0 - self-hosted)");

        return LlmCompletionResult.CreateSuccess(assistantMessage, usage, cost, metadata);
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
            _logger.LogWarning("Empty user prompt provided for Ollama streaming completion");
            yield break;
        }

        using var httpRequest = CreateChatRequest(model, systemPrompt, userPrompt, temperature, maxTokens, stream: true);

        _logger.LogInformation("Starting Ollama streaming completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
            model, temperature, maxTokens);

        HttpResponseMessage? response = null;

        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError("Ollama streaming API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(errorBody));
                response.Dispose();
                yield break;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed initiating Ollama streaming");
            response?.Dispose();
            yield break;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Ollama streaming request timed out");
            response?.Dispose();
            yield break;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Streaming generator boundary - must handle all errors gracefully
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initiating Ollama streaming");
            response?.Dispose();
            yield break;
        }
#pragma warning restore CA1031

        await foreach (var chunk in ProcessStreamResponseAsync(response, model, ct).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    private async IAsyncEnumerable<StreamChunk> ProcessStreamResponseAsync(
        HttpResponseMessage response,
        string model,
        [EnumeratorCancellation] CancellationToken ct)
    {
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

                // Ollama streams JSON objects line-by-line (not SSE format)
                OllamaStreamChunk? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Ollama streaming chunk: {Data}", line);
                    continue;
                }

                // Regular content chunk (process first, even if done=true)
                var delta = chunk?.Message?.Content;
                var hasContent = !string.IsNullOrEmpty(delta);

                if (hasContent)
                {
                    yield return new StreamChunk(Content: delta);
                }

                // ISSUE-1725: Check if this is the final chunk (done=true)
                if (chunk?.Done is true)
                {
                    // Final chunk - may contain usage metadata
                    if (chunk.PromptEvalCount.HasValue && chunk.EvalCount.HasValue)
                    {
                        var totalTokens = chunk.PromptEvalCount.Value + chunk.EvalCount.Value;
                        var usage = new LlmUsage(
                            chunk.PromptEvalCount.Value,
                            chunk.EvalCount.Value,
                            totalTokens);

                        var cost = _costCalculator.CalculateCost(
                            chunk.Model ?? model,
                            ProviderName,
                            usage.PromptTokens,
                            usage.CompletionTokens);

                        var llmCost = new LlmCost
                        {
                            InputCost = cost.InputCost,
                            OutputCost = cost.OutputCost,
                            ModelId = chunk.Model ?? model,
                            Provider = ProviderName
                        };

                        _logger.LogInformation(
                            "Ollama streaming usage: {PromptTokens}p + {CompletionTokens}c = {TotalTokens}t (${TotalCost:F6})",
                            usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens, llmCost.TotalCost);

                        yield return new StreamChunk(
                            Content: null,
                            Usage: usage,
                            Cost: llmCost,
                            IsFinal: true);
                    }
                    else
                    {
                        // Done but no usage metadata (fallback)
                        _logger.LogWarning("Ollama streaming finished without usage metadata");
                    }

                    _logger.LogInformation("Ollama streaming completion finished");
                    break; // Exit after processing final chunk
                }
            }
        }
    }

    /// <summary>
    /// Estimate token count using word-based heuristic (~0.75 tokens per word)
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)(wordCount * 0.75);
    }
}

// Ollama API response models
internal record OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; init; }

    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }
}

internal record OllamaMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

// Streaming response models
internal record OllamaStreamChunk
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }

    /// <summary>
    /// ISSUE-1725: Usage metadata in final chunk (when done=true)
    /// Ollama provides eval_count (completion), prompt_eval_count (prompt)
    /// </summary>
    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; init; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; init; }
}
