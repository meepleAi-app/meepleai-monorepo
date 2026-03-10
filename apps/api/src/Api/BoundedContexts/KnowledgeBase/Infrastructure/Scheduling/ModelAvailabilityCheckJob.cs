using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that verifies all configured LLM models are still available on OpenRouter.
/// Issue #5493: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// Runs every 6 hours. Detects deprecated/unavailable models and publishes ModelDeprecatedEvent.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class ModelAvailabilityCheckJob : IJob
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStrategyModelMappingRepository _strategyMappingRepository;
    private readonly IModelCompatibilityRepository _compatibilityRepository;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ModelAvailabilityCheckJob> _logger;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);

    public ModelAvailabilityCheckJob(
        IHttpClientFactory httpClientFactory,
        IStrategyModelMappingRepository strategyMappingRepository,
        IModelCompatibilityRepository compatibilityRepository,
        IMediator mediator,
        IConfiguration configuration,
        ILogger<ModelAvailabilityCheckJob> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _strategyMappingRepository = strategyMappingRepository ?? throw new ArgumentNullException(nameof(strategyMappingRepository));
        _compatibilityRepository = compatibilityRepository ?? throw new ArgumentNullException(nameof(compatibilityRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "Starting model availability check: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // Step 1: Fetch available models from OpenRouter
            var availableModels = await FetchOpenRouterModelsAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (availableModels == null)
            {
                _logger.LogWarning("Failed to fetch OpenRouter model list — skipping availability check");
                context.Result = new { Success = false, Reason = "OpenRouter API unavailable" };
                return;
            }

            var availableModelIds = new HashSet<string>(
                availableModels.Select(m => m.Id),
                StringComparer.OrdinalIgnoreCase);

            // O(1) lookup dictionary for model info (avoids O(N) FirstOrDefault in loop)
            var modelInfoLookup = availableModels.ToDictionary(
                m => m.Id,
                m => m,
                StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Fetched {Count} models from OpenRouter", availableModelIds.Count);

            // Step 2: Get all strategy-model mappings from DB
            var mappings = await _strategyMappingRepository.GetAllAsync(context.CancellationToken)
                .ConfigureAwait(false);

            var openRouterMappings = mappings
                .Where(m => string.Equals(m.Provider, "openrouter", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogInformation("Checking {Count} OpenRouter strategy mappings", openRouterMappings.Count);

            // Step 3: Check each configured model
            var availableCount = 0;
            var deprecatedCount = 0;
            var unavailableCount = 0;

            // Collect all unique model IDs (primary + fallbacks)
            var allConfiguredModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in openRouterMappings)
            {
                allConfiguredModels.Add(mapping.PrimaryModel);
                foreach (var fallback in mapping.FallbackModels)
                {
                    allConfiguredModels.Add(fallback);
                }
            }

            foreach (var modelId in allConfiguredModels)
            {
                var isAvailable = availableModelIds.Contains(modelId);
                modelInfoLookup.TryGetValue(modelId, out var openRouterModel);

                if (isAvailable)
                {
                    availableCount++;

                    // Update compatibility matrix with current info
                    await UpsertCompatibilityEntryAsync(modelId, openRouterModel!, true, false, context.CancellationToken)
                        .ConfigureAwait(false);

                    // Update availability status (may restore previously unavailable model)
                    await _compatibilityRepository.UpdateAvailabilityAsync(
                        modelId, true, false, context.CancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    unavailableCount++;

                    // Find affected strategies
                    var affectedStrategies = openRouterMappings
                        .Where(m => string.Equals(m.PrimaryModel, modelId, StringComparison.OrdinalIgnoreCase))
                        .Select(m => m.Strategy)
                        .ToArray();

                    // Find suggested replacement from compatibility matrix
                    var compatEntry = await _compatibilityRepository.GetByModelIdAsync(
                        modelId, context.CancellationToken).ConfigureAwait(false);
                    var suggestedReplacement = compatEntry?.Alternatives
                        .FirstOrDefault(alt => availableModelIds.Contains(alt));

                    // Update availability
                    await _compatibilityRepository.UpdateAvailabilityAsync(
                        modelId, false, false, context.CancellationToken)
                        .ConfigureAwait(false);

                    // Log change
                    await _compatibilityRepository.LogChangeAsync(
                        new ModelChangeLogEntry(
                            Guid.NewGuid(),
                            modelId,
                            "unavailable",
                            null,
                            suggestedReplacement,
                            affectedStrategies.Length > 0 ? affectedStrategies[0] : null,
                            "Model not found in OpenRouter model list",
                            true,
                            null,
                            DateTime.UtcNow),
                        context.CancellationToken).ConfigureAwait(false);

                    // Publish event for notification + auto-fallback handlers
                    if (affectedStrategies.Length > 0)
                    {
                        deprecatedCount++;

                        await _mediator.Publish(
                            new ModelDeprecatedEvent(
                                modelId,
                                "openrouter",
                                affectedStrategies,
                                suggestedReplacement,
                                "Model no longer available on OpenRouter",
                                DateTime.UtcNow),
                            context.CancellationToken).ConfigureAwait(false);

                        _logger.LogWarning(
                            "Model {ModelId} unavailable — affects strategies: [{Strategies}], suggested replacement: {Replacement}",
                            modelId,
                            string.Join(", ", affectedStrategies),
                            suggestedReplacement ?? "none");
                    }
                }
            }

            _logger.LogInformation(
                "Model availability check completed: Available={Available}, Deprecated={Deprecated}, Unavailable={Unavailable}",
                availableCount, deprecatedCount, unavailableCount);

            context.Result = new
            {
                Success = true,
                Available = availableCount,
                Deprecated = deprecatedCount,
                Unavailable = unavailableCount,
                TotalChecked = allConfiguredModels.Count,
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125
        // BACKGROUND SERVICE: Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model availability check job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Fetches the list of available models from OpenRouter API.
    /// Returns null if the API is unreachable or returns an error.
    /// </summary>
    internal async Task<IReadOnlyList<OpenRouterModelInfo>?> FetchOpenRouterModelsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("OpenRouter");

            var apiKey = SecretsHelper.GetSecretOrValue(
                _configuration, "OPENROUTER_API_KEY", _logger, required: false);

            using var request = new HttpRequestMessage(HttpMethod.Get, "models");
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(RequestTimeout);

            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenRouter models API returned {StatusCode}",
                    response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<OpenRouterModelsResponse>(json);

            return result?.Data;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "OpenRouter models API request timed out");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to OpenRouter models API");
            return null;
        }
    }

    private async Task UpsertCompatibilityEntryAsync(
        string modelId,
        OpenRouterModelInfo model,
        bool isAvailable,
        bool isDeprecated,
        CancellationToken cancellationToken)
    {
        var existing = await _compatibilityRepository.GetByModelIdAsync(modelId, cancellationToken)
            .ConfigureAwait(false);

        var entry = new ModelCompatibilityEntry(
            existing?.Id ?? Guid.NewGuid(),
            modelId,
            model.Name ?? modelId,
            "openrouter",
            existing?.Alternatives ?? Array.Empty<string>(),
            model.ContextLength ?? 0,
            existing?.Strengths ?? Array.Empty<string>(),
            isAvailable,
            isDeprecated,
            DateTime.UtcNow);

        await _compatibilityRepository.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    // ── OpenRouter API response DTOs ──

    internal sealed record OpenRouterModelsResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<OpenRouterModelInfo>? Data);

    internal sealed record OpenRouterModelInfo(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("context_length")] int? ContextLength,
        [property: JsonPropertyName("pricing")] OpenRouterPricing? Pricing);

    internal sealed record OpenRouterPricing(
        [property: JsonPropertyName("prompt")] string? Prompt,
        [property: JsonPropertyName("completion")] string? Completion);
}
