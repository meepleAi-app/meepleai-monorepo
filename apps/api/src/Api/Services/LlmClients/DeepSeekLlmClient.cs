using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Services.LlmClients;

/// <summary>
/// LLM client for DeepSeek API (https://api.deepseek.com)
/// ISSUE-419: Direct DeepSeek provider for cost-effective high-quality inference
/// </summary>
/// <remarks>
/// DeepSeek provides an OpenAI-compatible API for their models (deepseek-chat, deepseek-reasoner, etc.)
/// Used as an alternative to routing through OpenRouter for DeepSeek models.
/// Includes usage metadata in streaming responses by default (no explicit usage.include needed).
///
/// Issue #1859: API key is resolved at call time via <see cref="IProviderCredentialResolver"/>
/// (DB → env-var cascade with 5-min cache). The client is registered as a singleton, so the
/// resolver is fetched from a fresh scope via <see cref="IServiceScopeFactory"/> per HTTP send.
/// When neither DB row nor env var is configured the resolver throws
/// <see cref="ProviderCredentialNotConfiguredException"/>; the call paths convert this into a
/// graceful failure (return-failure for completion, yield-break for streaming).
/// </remarks>
internal class DeepSeekLlmClient : ILlmClient
{
    internal const string ProviderKey = "deepseek";

    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeepSeekLlmClient> _logger;
    private readonly ILlmCostCalculator _costCalculator;

    // Hardcoded defaults
    private const int DefaultTimeoutSeconds = 60;

    public string ProviderName => "DeepSeek";

    public DeepSeekLlmClient(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILlmCostCalculator costCalculator,
        ILogger<DeepSeekLlmClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(costCalculator);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClientFactory.CreateClient("DeepSeek");
        _scopeFactory = scopeFactory;
        _logger = logger;
        _costCalculator = costCalculator;

        // S1075: DeepSeek API endpoint (official public endpoint)
#pragma warning disable S1075 // URIs should not be hardcoded - Official API endpoint
        const string DeepSeekApiBaseUrl = "https://api.deepseek.com/";
#pragma warning restore S1075

        _httpClient.BaseAddress = new Uri(DeepSeekApiBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

        _logger.LogInformation("DeepSeekLlmClient initialized with cost tracking (key resolved on demand)");
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
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return LlmCompletionResult.CreateFailure("No user prompt provided");
        }

        string apiKey;
        try
        {
            apiKey = await ResolveApiKeyAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderCredentialNotConfiguredException ex)
        {
            _logger.LogWarning(ex, "DeepSeek provider is not configured (no DB row and no env var)");
            return LlmCompletionResult.CreateFailure("DeepSeek provider is not configured (missing API key)");
        }

        try
        {
            using var httpRequest = CreateChatRequest(model, systemPrompt, userPrompt, temperature, maxTokens, stream: false);
            ApplyAuthorization(httpRequest, apiKey);

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

        string apiKey;
        try
        {
            apiKey = await ResolveApiKeyAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderCredentialNotConfiguredException ex)
        {
            _logger.LogWarning(ex, "DeepSeek provider is not configured (no DB row and no env var)");
            return LlmCompletionResult.CreateFailure("DeepSeek provider is not configured (missing API key)");
        }

        try
        {
            using var httpRequest = CreateMultimodalChatRequest(model, messages, temperature, maxTokens, stream: false);
            ApplyAuthorization(httpRequest, apiKey);

            _logger.LogInformation("Generating DeepSeek multimodal completion using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
                model, temperature, maxTokens);

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            return await HandleCompletionResponseAsync(response, model, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "DeepSeek multimodal completion timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during DeepSeek multimodal completion");
            return LlmCompletionResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize DeepSeek multimodal response");
            return LlmCompletionResult.CreateFailure("Invalid response format");
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Wraps unexpected DeepSeek API errors into domain-friendly LlmCompletionResult
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during DeepSeek multimodal completion");
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
            _logger.LogWarning("No messages provided for DeepSeek multimodal streaming");
            yield break;
        }

        var apiKey = await ResolveApiKeyOrLogAsync(ct).ConfigureAwait(false);
        if (apiKey is null)
        {
            yield break;
        }

        _logger.LogInformation("Starting DeepSeek multimodal streaming using {Model} (temp={Temperature}, max_tokens={MaxTokens})",
            model, temperature, maxTokens);

        const int maxRetries = 3;
        int[] retryDelaysMs = [3000, 5000, 10000];
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var httpRequest = CreateMultimodalChatRequest(model, messages, temperature, maxTokens, stream: true);
            ApplyAuthorization(httpRequest, apiKey);
            response = null;

            try
            {
                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError("DeepSeek multimodal streaming API error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(errorBody));

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
                _logger.LogError(ex, "HTTP request failed initiating DeepSeek multimodal streaming");
                response?.Dispose();
                response = null;
                yield break;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "DeepSeek multimodal streaming request timed out");
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
                _logger.LogError(ex, "Unexpected error initiating DeepSeek multimodal streaming");
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
                // Text-only message — use simple string content format
                var textContent = string.Join("\n", message.Content.OfType<TextContentPart>().Select(t => t.Text));
                apiMessages.Add(new { role = message.Role, content = textContent });
            }
            else
            {
                // Multimodal message — use content array format
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

        var json = JsonSerializer.Serialize(requestPayload);
        return new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    /// <inheritdoc/>
    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        string apiKey;
        try
        {
            apiKey = await ResolveApiKeyAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderCredentialNotConfiguredException)
        {
            return false;
        }

        try
        {
            // #2953 (#5): GET /models returns 200 even when the account is out of credit — the
            // 402 only surfaces on chat/completions, so /models is blind to exhaustion. Query
            // /user/balance instead: is_available=false means the balance can't cover paid calls
            // (credit exhausted), so the provider must be reported unhealthy. Zero token cost.
            using var request = new HttpRequestMessage(HttpMethod.Get, "user/balance");
            ApplyAuthorization(request, apiKey);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("is_available", out var isAvailable)
                && isAvailable.ValueKind == JsonValueKind.False)
            {
                _logger.LogWarning("DeepSeek health check: account balance not available (credit exhausted).");
                return false;
            }

            return true;
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
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("Empty user prompt provided for DeepSeek streaming");
            yield break;
        }

        var apiKey = await ResolveApiKeyOrLogAsync(ct).ConfigureAwait(false);
        if (apiKey is null)
        {
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
            ApplyAuthorization(httpRequest, apiKey);
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

                        var finishReason = chunk.Choices?[0]?.FinishReason;

                        yield return new StreamChunk(
                            Content: null,
                            Usage: usage,
                            Cost: llmCost,
                            IsFinal: true,
                            FinishReason: finishReason);
                        break; // Usage signals end of stream
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resolves the DeepSeek API key via a fresh DI scope (resolver is Scoped, this client is Singleton).
    /// Throws <see cref="ProviderCredentialNotConfiguredException"/> when neither DB row nor env var exists.
    /// </summary>
    private async Task<string> ResolveApiKeyAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IProviderCredentialResolver>();
        return await resolver.ResolveAsync(ProviderKey, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Streaming-friendly variant of <see cref="ResolveApiKeyAsync"/>: returns <c>null</c> instead of
    /// throwing when the provider is not configured, allowing callers to <c>yield break</c> cleanly.
    /// </summary>
    private async Task<string?> ResolveApiKeyOrLogAsync(CancellationToken ct)
    {
        try
        {
            return await ResolveApiKeyAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderCredentialNotConfiguredException ex)
        {
            _logger.LogWarning(ex, "DeepSeek provider is not configured — streaming unavailable");
            return null;
        }
    }

    private static void ApplyAuthorization(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }
}
