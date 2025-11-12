using System.Diagnostics;
using System.Runtime.CompilerServices;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Hybrid LLM service coordinating multiple providers (Ollama, OpenRouter) with adaptive routing
/// ISSUE-958: Implements hybrid architecture with user-tier based routing and traffic split
/// </summary>
/// <remarks>
/// Architecture:
/// - Coordinates ILlmClient implementations (OllamaLlmClient, OpenRouterLlmClient)
/// - Uses ILlmRoutingStrategy for provider selection (user-type + traffic split)
/// - Implements ILlmService interface for seamless integration with existing code
/// - Supports both completion and streaming modes
///
/// Cost Optimization:
/// - Target: 80% free tier (Ollama/Llama 3.3 70B free), 20% paid (GPT-4o-mini)
/// - Anonymous/User: Heavy Ollama usage
/// - Editor: Balanced
/// - Admin: Premium models prioritized
/// </remarks>
public class HybridLlmService : ILlmService
{
    private readonly IEnumerable<ILlmClient> _clients;
    private readonly ILlmRoutingStrategy _routingStrategy;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly ILogger<HybridLlmService> _logger;

    // Default LLM parameters
    private const double DefaultTemperature = 0.3;
    private const int DefaultMaxTokens = 500;

    public HybridLlmService(
        IEnumerable<ILlmClient> clients,
        ILlmRoutingStrategy routingStrategy,
        ILlmCostLogRepository costLogRepository,
        ILogger<HybridLlmService> logger)
    {
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
        _routingStrategy = routingStrategy ?? throw new ArgumentNullException(nameof(routingStrategy));
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_clients.Any())
        {
            throw new InvalidOperationException("At least one ILlmClient must be registered");
        }

        _logger.LogInformation(
            "HybridLlmService initialized with {ClientCount} providers: {Providers} (cost tracking enabled)",
            _clients.Count(),
            string.Join(", ", _clients.Select(c => c.ProviderName)));
    }

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        return await GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            user: null, // No user context (anonymous)
            ct);
    }

    /// <summary>
    /// Generate completion with user context for adaptive routing
    /// </summary>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        User? user,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return LlmCompletionResult.CreateFailure("No user prompt provided");
        }

        // Route to appropriate provider
        var decision = _routingStrategy.SelectProvider(user);
        var client = GetClient(decision.ProviderName);

        if (client == null)
        {
            _logger.LogError(
                "No client found for provider {Provider}, falling back to first available",
                decision.ProviderName);
            client = _clients.First();
        }

        _logger.LogInformation(
            "Generating completion via {Provider} ({Model}) - Reason: {Reason}",
            client.ProviderName, decision.ModelId, decision.Reason);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await client.GenerateCompletionAsync(
                decision.ModelId,
                systemPrompt,
                userPrompt,
                DefaultTemperature,
                DefaultMaxTokens,
                ct);

            stopwatch.Stop();

            // Add routing metadata
            if (result.Success && result.Metadata is Dictionary<string, string> metadata)
            {
                metadata["routing_decision"] = decision.Reason;
                metadata["selected_provider"] = client.ProviderName;
                metadata["selected_model"] = decision.ModelId;
            }

            // ISSUE-960: Log cost to database (fire and forget - don't block response)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _costLogRepository.LogCostAsync(
                        user?.Id,
                        user?.Role.Value ?? "Anonymous",
                        new Domain.Models.LlmCostCalculation
                        {
                            ModelId = result.Cost.ModelId,
                            Provider = result.Cost.Provider,
                            PromptTokens = result.Usage.PromptTokens,
                            CompletionTokens = result.Usage.CompletionTokens,
                            InputCost = result.Cost.InputCost,
                            OutputCost = result.Cost.OutputCost
                        },
                        endpoint: "completion",
                        success: result.Success,
                        errorMessage: result.ErrorMessage,
                        latencyMs: (int)stopwatch.ElapsedMilliseconds,
                        ipAddress: null, // Not available here - would come from HTTP context
                        userAgent: null,
                        ct: CancellationToken.None);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log LLM cost (non-blocking)");
                }
            }, CancellationToken.None);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Error generating completion with {Provider} ({Model})",
                client.ProviderName, decision.ModelId);

            // ISSUE-960: Log failed request cost (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _costLogRepository.LogCostAsync(
                        user?.Id,
                        user?.Role.Value ?? "Anonymous",
                        Domain.Models.LlmCostCalculation.Empty,
                        endpoint: "completion",
                        success: false,
                        errorMessage: ex.Message,
                        latencyMs: (int)stopwatch.ElapsedMilliseconds,
                        ipAddress: null,
                        userAgent: null,
                        ct: CancellationToken.None);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log LLM error cost (non-blocking)");
                }
            }, CancellationToken.None);

            return LlmCompletionResult.CreateFailure($"Provider error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        return GenerateCompletionStreamAsync(
            systemPrompt,
            userPrompt,
            user: null, // No user context (anonymous)
            ct);
    }

    /// <summary>
    /// Generate streaming completion with user context for adaptive routing
    /// </summary>
    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        User? user,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("Empty user prompt for streaming completion");
            yield break;
        }

        // Route to appropriate provider
        var decision = _routingStrategy.SelectProvider(user);
        var client = GetClient(decision.ProviderName);

        if (client == null)
        {
            _logger.LogError(
                "No client found for provider {Provider}, falling back to first available",
                decision.ProviderName);
            client = _clients.First();
        }

        _logger.LogInformation(
            "Starting streaming completion via {Provider} ({Model}) - Reason: {Reason}",
            client.ProviderName, decision.ModelId, decision.Reason);

        await foreach (var chunk in client.GenerateCompletionStreamAsync(
            decision.ModelId,
            systemPrompt,
            userPrompt,
            DefaultTemperature,
            DefaultMaxTokens,
            ct))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc/>
    public async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default) where T : class
    {
        return await GenerateJsonAsync<T>(
            systemPrompt,
            userPrompt,
            user: null, // No user context (anonymous)
            ct);
    }

    /// <summary>
    /// Generate JSON response with user context for adaptive routing
    /// </summary>
    public async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        User? user,
        CancellationToken ct = default) where T : class
    {
        // Enhance system prompt for JSON mode
        var enhancedSystemPrompt = $"""
            {systemPrompt}

            CRITICAL: You must return ONLY valid JSON. No markdown code blocks, no explanations, no additional text.
            Just the raw JSON object that matches the required structure.
            """;

        var result = await GenerateCompletionAsync(enhancedSystemPrompt, userPrompt, user, ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
        {
            _logger.LogWarning("LLM completion failed or returned empty response for JSON generation");
            return null;
        }

        try
        {
            // Clean common LLM formatting artifacts
            var jsonText = CleanJsonResponse(result.Response);

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
            };

            var parsed = System.Text.Json.JsonSerializer.Deserialize<T>(jsonText, options);

            if (parsed == null)
            {
                _logger.LogWarning("LLM returned valid JSON but deserialization produced null");
            }

            return parsed;
        }
        catch (System.Text.Json.JsonException ex)
        {
            var truncated = result.Response.Length > 500
                ? result.Response.Substring(0, 500) + "..."
                : result.Response;
            _logger.LogWarning(ex, "Failed to parse LLM JSON response. Raw: {Response}", truncated);
            return null;
        }
    }

    /// <summary>
    /// Get LLM client by provider name
    /// </summary>
    private ILlmClient? GetClient(string providerName)
    {
        return _clients.FirstOrDefault(c =>
            c.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clean LLM response to extract pure JSON
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
