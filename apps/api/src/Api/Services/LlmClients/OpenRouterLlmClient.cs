using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Api.Models;

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
internal class OpenRouterLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterLlmClient> _logger;
    private readonly ILlmCostCalculator _costCalculator;

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

        // S1075: OpenRouter API endpoint (official public endpoint)
#pragma warning disable S1075 // URIs should not be hardcoded - Official API endpoint
        const string OpenRouterApiBaseUrl = "https://openrouter.ai/api/v1/";
#pragma warning restore S1075

        // SEC-708: Read API key from Docker Secret file or direct config (S1450: local scope)
        var apiKey = SecretsHelper.GetSecretOrValue(config, "OPENROUTER_API_KEY", logger, required: true)
            ?? throw new InvalidOperationException("OPENROUTER_API_KEY not configured");

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(OpenRouterApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
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
            using var httpRequest = CreateChatRequest(model, systemPrompt, userPrompt, temperature, maxTokens, stream: false);

            _logger.LogInformation("Generating OpenRouter completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
                model, temperature, maxTokens);

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            return await HandleCompletionResponseAsync(response, model, ct).ConfigureAwait(false);
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
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Wraps unexpected OpenRouter API errors into domain-friendly LlmCompletionResult
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OpenRouter completion");
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

        // Issue #3231: Use Dictionary to conditionally include 'usage' field
        // OpenRouter rejects "usage": null, so omit field entirely when not streaming
        var requestPayload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
            ["stream"] = stream
        };

        if (stream)
        {
            requestPayload["usage"] = new { include = true };
        }

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
            _logger.LogError("OpenRouter API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(responseBody));

            // Issue #5087: Parse 429/402 rate limit errors to surface metadata for fallback routing
            var rateLimitError = OpenRouterErrorParser.TryParseRateLimitError(responseBody, statusCode);
            if (rateLimitError != null)
            {
                var rlMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["rate_limit_type"] = rateLimitError.ErrorType.ToString().ToLowerInvariant()
                };
                if (rateLimitError.ResetTimestampMs.HasValue)
                    rlMetadata["rate_limit_reset_ms"] = rateLimitError.ResetTimestampMs.Value.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(rateLimitError.ModelId))
                    rlMetadata["rate_limit_model"] = rateLimitError.ModelId;

                _logger.LogWarning(
                    "OpenRouter rate limit: {ErrorType} for {Model} (resetMs={ResetMs})",
                    rateLimitError.ErrorType, rateLimitError.ModelId, rateLimitError.ResetTimestampMs);

                return LlmCompletionResult.CreateFailure(
                    $"OpenRouter rate limit: {rateLimitError.ErrorType} ({statusCode})")
                    with
                { Metadata = rlMetadata };
            }

            return LlmCompletionResult.CreateFailure($"OpenRouter API error: {statusCode} ({response.StatusCode})");
        }

        var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseBody);

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

    /// <inheritdoc/>
    public bool SupportsVision => true;

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        double temperature,
        int maxTokens,
        CancellationToken ct = default)
    {
        if (messages == null || messages.Count == 0)
        {
            return LlmCompletionResult.CreateFailure("No messages provided");
        }

        try
        {
            using var httpRequest = CreateMultimodalChatRequest(model, messages, temperature, maxTokens, stream: false);

            _logger.LogInformation("Generating OpenRouter multimodal completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
                model, temperature, maxTokens);

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            return await HandleCompletionResponseAsync(response, model, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "OpenRouter multimodal completion timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during OpenRouter multimodal completion");
            return LlmCompletionResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize OpenRouter multimodal response");
            return LlmCompletionResult.CreateFailure("Invalid response format");
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Wraps unexpected OpenRouter API errors into domain-friendly LlmCompletionResult
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OpenRouter multimodal completion");
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        double temperature,
        int maxTokens,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (messages == null || messages.Count == 0)
        {
            _logger.LogWarning("No messages provided for OpenRouter multimodal streaming");
            yield break;
        }

        _logger.LogInformation("Starting OpenRouter multimodal streaming using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
            model, temperature, maxTokens);

        const int maxRetries = 3;
        int[] retryDelaysMs = [3000, 5000, 10000];
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var httpRequest = CreateMultimodalChatRequest(model, messages, temperature, maxTokens, stream: true);
            response = null;

            try
            {
                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError("OpenRouter multimodal streaming API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(errorBody));

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

                response.Dispose();
                response = null;
                yield break;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed initiating OpenRouter multimodal streaming");
                response?.Dispose();
                response = null;
                yield break;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "OpenRouter multimodal streaming request timed out");
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
                _logger.LogError(ex, "Unexpected error initiating OpenRouter multimodal streaming");
                response?.Dispose();
                response = null;
                yield break;
            }
#pragma warning restore CA1031
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            _logger.LogError("All {MaxRetries} multimodal streaming retry attempts exhausted", maxRetries + 1);
            response?.Dispose();
            yield break;
        }

        await foreach (var chunk in ProcessStreamResponseAsync(response, model, ct).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    private HttpRequestMessage CreateMultimodalChatRequest(
        string model,
        IReadOnlyList<LlmMessage> messages,
        double temperature,
        int maxTokens,
        bool stream)
    {
        var apiMessages = new List<object>();

        foreach (var message in messages)
        {
            if (!message.HasImages)
            {
                var textContent = string.Join("\n", message.Content.OfType<TextContentPart>().Select(t => t.Text));
                apiMessages.Add(new { role = message.Role, content = textContent });
            }
            else
            {
                var contentParts = new List<object>();
                foreach (var part in message.Content)
                {
                    if (part is TextContentPart textPart)
                    {
                        contentParts.Add(new { type = "text", text = textPart.Text });
                    }
                    else if (part is ImageContentPart imagePart)
                    {
                        contentParts.Add(new
                        {
                            type = "image_url",
                            image_url = new { url = imagePart.ToDataUri() }
                        });
                    }
                }
                apiMessages.Add(new { role = message.Role, content = contentParts });
            }
        }

        var requestPayload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["model"] = model,
            ["messages"] = apiMessages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
            ["stream"] = stream
        };

        if (stream)
        {
            requestPayload["usage"] = new { include = true };
        }

        var json = JsonSerializer.Serialize(requestPayload);
        return new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    /// <inheritdoc/>
    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("models", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OpenRouter health check failed");
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
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("Empty user prompt provided for OpenRouter streaming");
            yield break;
        }

        _logger.LogInformation("Starting OpenRouter streaming completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
            model, temperature, maxTokens);

        // Retry loop for transient 429 (TooManyRequests) errors from OpenRouter free models.
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
                _logger.LogError("OpenRouter streaming API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(errorBody));

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
                _logger.LogError(ex, "HTTP request failed initiating OpenRouter streaming");
                response?.Dispose();
                response = null;
                yield break;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "OpenRouter streaming request timed out");
                response?.Dispose();
                response = null;
                yield break;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation initiating OpenRouter streaming");
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
                _logger.LogError(ex, "Unexpected error initiating OpenRouter streaming");
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

                    // OpenRouter sends "[DONE]" when stream is complete
                    if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
                    {
                        _logger.LogInformation("OpenRouter streaming finished");
                        break;
                    }

                    OpenAiStreamChunk? chunk = null;
                    try
                    {
                        chunk = JsonSerializer.Deserialize<OpenAiStreamChunk>(data);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse OpenRouter streaming chunk: {Data}", LogSanitizer.Sanitize(data));
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
