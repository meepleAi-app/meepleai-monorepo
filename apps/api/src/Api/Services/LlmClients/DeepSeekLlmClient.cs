using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Security;

namespace Api.Services.LlmClients;

/// <summary>
/// LLM client for DeepSeek API (https://api.deepseek.com)
/// ISSUE-419: Direct DeepSeek provider for cost-effective high-quality inference
/// </summary>
/// <remarks>
/// DeepSeek provides an OpenAI-compatible API for their models (deepseek-chat, deepseek-reasoner, etc.)
/// Used as an alternative to routing through OpenRouter for DeepSeek models.
/// Includes usage metadata in streaming responses by default (no explicit usage.include needed).
/// </remarks>
internal class DeepSeekLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekLlmClient> _logger;
    private readonly ILlmCostCalculator _costCalculator;
    private readonly bool _isConfigured;

    // Hardcoded defaults
    private const int DefaultTimeoutSeconds = 60;

    public string ProviderName => "DeepSeek";

    public DeepSeekLlmClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILlmCostCalculator costCalculator,
        ILogger<DeepSeekLlmClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("DeepSeek");
        _logger = logger;
        _costCalculator = costCalculator;

        // S1075: DeepSeek API endpoint (official public endpoint)
#pragma warning disable S1075 // URIs should not be hardcoded - Official API endpoint
        const string DeepSeekApiBaseUrl = "https://api.deepseek.com/";
#pragma warning restore S1075

        // Read API key from Docker Secret file or direct config — optional provider
        var apiKey = SecretsHelper.GetSecretOrValue(config, "DEEPSEEK_API_KEY", logger, required: false);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("DEEPSEEK_API_KEY not configured — DeepSeek provider will be unavailable");
            _isConfigured = false;
            return;
        }

        _isConfigured = true;

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(DeepSeekApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

        _logger.LogInformation("DeepSeekLlmClient initialized with cost tracking");
    }

    /// <inheritdoc/>
    public bool SupportsModel(string modelId)
    {
        // DeepSeek models: deepseek-chat, deepseek-reasoner, etc.
        return modelId.StartsWith("deepseek-", StringComparison.OrdinalIgnoreCase);
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
        if (!_isConfigured)
        {
            return LlmCompletionResult.CreateFailure("DeepSeek provider is not configured (missing API key)");
        }

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return LlmCompletionResult.CreateFailure("No user prompt provided");
        }

        try
        {
            using var httpRequest = CreateChatRequest(model, systemPrompt, userPrompt, temperature, maxTokens, stream: false);

            _logger.LogInformation("Generating DeepSeek completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
                model, temperature, maxTokens);

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            return await HandleCompletionResponseAsync(response, model, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "DeepSeek completion timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during DeepSeek completion");
            return LlmCompletionResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize DeepSeek response");
            return LlmCompletionResult.CreateFailure("Invalid response format");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during DeepSeek completion");
            return LlmCompletionResult.CreateFailure($"Configuration error: {ex.Message}");
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Wraps unexpected DeepSeek API errors into domain-friendly LlmCompletionResult
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during DeepSeek completion");
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

        // DeepSeek uses standard OpenAI format — no 'usage' field needed for streaming
        // (DeepSeek includes usage metadata in the final SSE chunk by default)
        var requestPayload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
            ["stream"] = stream
        };

        var json = JsonSerializer.Serialize(requestPayload);
        return new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private async Task<LlmCompletionResult> HandleCompletionResponseAsync(
        HttpResponseMessage response,
        string model,
        CancellationToken ct)
    {
        // CodeQL: cs/cleartext-storage-of-sensitive-information — response body is read for
        // deserialization and error logging (masked via DataMasking.MaskResponseBody). Suppressing as by-design.
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            _logger.LogError("DeepSeek API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(responseBody));
            return LlmCompletionResult.CreateFailure($"DeepSeek API error: {statusCode} ({response.StatusCode})");
        }

        var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseBody);

        if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
        {
            return LlmCompletionResult.CreateFailure("No response returned from DeepSeek");
        }

        var assistantMessage = chatResponse.Choices[0].Message?.Content ?? string.Empty;

        var usage = chatResponse.Usage != null
            ? new LlmUsage(
                chatResponse.Usage.PromptTokens,
                chatResponse.Usage.CompletionTokens,
                chatResponse.Usage.TotalTokens)
            : LlmUsage.Empty;

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
            ["provider"] = "DeepSeek",
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

        _logger.LogInformation("Successfully generated DeepSeek completion (cost: ${Cost:F6})", cost.TotalCost);

        return LlmCompletionResult.CreateSuccess(assistantMessage, usage, cost, metadata);
    }

    /// <inheritdoc/>
    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        if (!_isConfigured)
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.GetAsync("models", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DeepSeek health check failed");
            return false;
        }
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
        if (!_isConfigured)
        {
            _logger.LogWarning("DeepSeek provider is not configured — streaming unavailable");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("Empty user prompt provided for DeepSeek streaming");
            yield break;
        }

        _logger.LogInformation("Starting DeepSeek streaming completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
            model, temperature, maxTokens);

        // Retry loop for transient 429 (TooManyRequests) errors.
        // The retry wraps only the HTTP send + status check; yield return stays outside try-catch
        // to satisfy the C# async-iterator constraint.
        const int maxRetries = 3;
        int[] retryDelaysMs = [3000, 5000, 10000];
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var httpRequest = CreateChatRequest(model, systemPrompt, userPrompt, temperature, maxTokens, stream: true);
            response = null;

            try
            {
                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    break; // success — proceed to streaming below
                }

                // CodeQL: cs/cleartext-storage-of-sensitive-information — error body is masked before logging
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError("DeepSeek streaming API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(errorBody));

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < maxRetries)
                {
                    var delay = retryDelaysMs[Math.Min(attempt, retryDelaysMs.Length - 1)];
                    _logger.LogWarning("Rate limited (429), retrying in {Delay}ms (attempt {Next}/{Total})",
                        delay, attempt + 2, maxRetries + 1);
                    response.Dispose();
                    response = null;
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                // Non-retryable error or final attempt — give up
                response.Dispose();
                response = null;
                yield break;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed initiating DeepSeek streaming");
                response?.Dispose();
                response = null;
                yield break;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "DeepSeek streaming request timed out");
                response?.Dispose();
                response = null;
                yield break;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation initiating DeepSeek streaming");
                response?.Dispose();
                response = null;
                yield break;
            }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // SERVICE BOUNDARY: Streaming generator boundary - must handle all errors gracefully
#pragma warning restore S125
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error initiating DeepSeek streaming");
                response?.Dispose();
                response = null;
                yield break;
            }
#pragma warning restore CA1031
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            _logger.LogError("All {MaxRetries} streaming retry attempts exhausted for 429 rate limit", maxRetries + 1);
            response?.Dispose();
            yield break;
        }

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

                // SSE format: "data: {json}"
                if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    var data = line.Substring(6).Trim();

                    // DeepSeek sends "[DONE]" when stream is complete
                    if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
                    {
                        _logger.LogInformation("DeepSeek streaming finished");
                        break;
                    }

                    OpenAiStreamChunk? chunk = null;
                    try
                    {
                        chunk = JsonSerializer.Deserialize<OpenAiStreamChunk>(data);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse DeepSeek streaming chunk: {Data}", LogSanitizer.Sanitize(data));
                        continue;
                    }

                    // Regular content chunk (may be present even with usage)
                    var delta = chunk?.Choices?[0]?.Delta?.Content;
                    var hasContent = !string.IsNullOrEmpty(delta);

                    // Check for usage metadata in chunk (final chunk — DeepSeek includes by default)
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
                            "DeepSeek streaming usage: {PromptTokens}p + {CompletionTokens}c = {TotalTokens}t (${TotalCost:F6})",
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
