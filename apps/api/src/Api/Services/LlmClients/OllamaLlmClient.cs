using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Infrastructure.Security;

namespace Api.Services.LlmClients;

/// <summary>
/// LLM client for Ollama local models (http://ollama:11434)
/// ISSUE-958: Hybrid LLM architecture - Ollama provider for cost-effective inference
/// </summary>
/// <remarks>
/// Ollama provides OpenAI-compatible API format for local model inference.
/// Supports models like llama3:8b, llama3.1:8b, mistral, etc.
/// Used for anonymous/low-tier users or A/B testing traffic routing.
/// </remarks>
public class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLlmClient> _logger;

    // Hardcoded defaults for Ollama
    private const int DefaultTimeoutSeconds = 60;

    public string ProviderName => "Ollama";

    public OllamaLlmClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<OllamaLlmClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _logger = logger;

        // Configure Ollama endpoint (http://ollama:11434)
        var ollamaUrl = config["OllamaUrl"] ?? "http://ollama:11434";
        _httpClient.BaseAddress = new Uri(ollamaUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

        _logger.LogInformation("OllamaLlmClient initialized with endpoint: {OllamaUrl}", ollamaUrl);
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
            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            messages.Add(new { role = "user", content = userPrompt });

            // Ollama API format (OpenAI-compatible)
            var request = new
            {
                model = model,
                messages = messages,
                options = new
                {
                    temperature = temperature,
                    num_predict = maxTokens // Ollama's equivalent of max_tokens
                },
                stream = false
            };

            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Generating Ollama completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
                model, temperature, maxTokens);

            // Ollama endpoint: /api/chat
            using var response = await _httpClient.PostAsync("api/chat", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(responseBody));
                return LlmCompletionResult.CreateFailure($"Ollama API error: {response.StatusCode}");
            }

            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody);

            if (chatResponse?.Message == null)
            {
                return LlmCompletionResult.CreateFailure("No response returned from Ollama");
            }

            var assistantMessage = chatResponse.Message.Content ?? string.Empty;

            // Ollama doesn't provide detailed token counts in chat API (only in generate API)
            // Use estimated token counts: ~0.75 tokens per word
            var estimatedPromptTokens = EstimateTokenCount(systemPrompt + userPrompt);
            var estimatedCompletionTokens = EstimateTokenCount(assistantMessage);

            var usage = new LlmUsage(
                estimatedPromptTokens,
                estimatedCompletionTokens,
                estimatedPromptTokens + estimatedCompletionTokens);

            var metadata = new Dictionary<string, string>
            {
                ["model"] = chatResponse.Model ?? model,
                ["provider"] = "Ollama"
            };

            if (!string.IsNullOrWhiteSpace(chatResponse.DoneReason))
            {
                metadata["finish_reason"] = chatResponse.DoneReason;
            }

            _logger.LogInformation("Successfully generated Ollama completion");

            return LlmCompletionResult.CreateSuccess(assistantMessage, usage, metadata);
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
        // Justification: Service boundary using Result pattern - must return failure instead of throwing
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Ollama completion");
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(
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
            options = new
            {
                temperature = temperature,
                num_predict = maxTokens
            },
            stream = true // Enable streaming
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Starting Ollama streaming completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
            model, temperature, maxTokens);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
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
        // Justification: Streaming generator boundary - must handle all errors gracefully
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initiating Ollama streaming");
            response?.Dispose();
            yield break;
        }
#pragma warning restore CA1031

        // Process stream
        using (response)
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);

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

                // Ollama sends done=true when stream is complete
                if (chunk?.Done == true)
                {
                    _logger.LogInformation("Ollama streaming completion finished");
                    break;
                }

                var delta = chunk?.Message?.Content;
                if (!string.IsNullOrEmpty(delta))
                {
                    yield return delta;
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
}
