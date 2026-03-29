using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.BusinessSimulations.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #5489: Encapsulates all cost logging, budget tracking, and usage stats
/// extracted from HybridLlmService.
/// </summary>
internal sealed class LlmCostService : ILlmCostService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOpenRouterFileLogger? _openRouterFileLogger;
    private readonly IOpenRouterUsageService? _openRouterUsageService;
    private readonly ILogger<LlmCostService> _logger;

    public LlmCostService(
        IServiceScopeFactory scopeFactory,
        ILogger<LlmCostService> logger,
        IOpenRouterFileLogger? openRouterFileLogger = null,
        IOpenRouterUsageService? openRouterUsageService = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _openRouterFileLogger = openRouterFileLogger;
        _openRouterUsageService = openRouterUsageService;
    }

    /// <inheritdoc/>
    public async Task LogSuccessAsync(
        LlmCompletionResult result,
        LlmUserContext userContext,
        long latencyMs,
        RequestSource source,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Log cost to DB
            using (var scope = _scopeFactory.CreateScope())
            {
                var costLogRepo = scope.ServiceProvider.GetRequiredService<ILlmCostLogRepository>();
                await costLogRepo.LogCostAsync(
                    userContext.UserId,
                    userContext.RoleName ?? "Anonymous",
                    new LlmCostCalculation
                    {
                        ModelId = result.Cost.ModelId,
                        Provider = result.Cost.Provider,
                        PromptTokens = result.Usage.PromptTokens,
                        CompletionTokens = result.Usage.CompletionTokens,
                        InputCost = result.Cost.InputCost,
                        OutputCost = result.Cost.OutputCost
                    },
                    endpoint: "completion",
                    success: true,
                    errorMessage: null,
                    latencyMs: (int)latencyMs,
                    ipAddress: null,
                    userAgent: null,
                    source: source,
                    cancellationToken: ct).ConfigureAwait(false);
            }

            // 2. Update model usage stats in DB
            await UpdateModelUsageStatsAsync(result, ct).ConfigureAwait(false);

            // 3. Record user budget usage
            if (userContext.UserId != null)
            {
                using var budgetScope = _scopeFactory.CreateScope();
                var budgetService = budgetScope.ServiceProvider.GetService<IUserBudgetService>();
                if (budgetService != null)
                {
                    var requestCost = result.Cost.TotalCost;
                    var requestTokens = result.Usage.PromptTokens + result.Usage.CompletionTokens;
                    await budgetService
                        .RecordUsageAsync(userContext.UserId.Value, requestCost, requestTokens, ct)
                        .ConfigureAwait(false);
                }
            }

            // 4. Publish token usage ledger event
            var totalCost = result.Cost.InputCost + result.Cost.OutputCost;
            if (totalCost > 0 && userContext.UserId != null)
            {
                using var publishScope = _scopeFactory.CreateScope();
                var publisher = publishScope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(
                    new TokenUsageLedgerEvent(
                        UserId: userContext.UserId.Value,
                        ModelId: result.Cost.ModelId,
                        TokensConsumed: result.Usage.PromptTokens + result.Usage.CompletionTokens,
                        CostUsd: totalCost,
                        Endpoint: "completion",
                        Timestamp: DateTime.UtcNow),
                    ct).ConfigureAwait(false);
            }

            // 5. File logger
            _openRouterFileLogger?.LogRequest(
                requestId: Guid.NewGuid().ToString(),
                model: result.Cost.ModelId,
                provider: result.Cost.Provider,
                source: source.ToString(),
                userId: userContext.UserId,
                promptTokens: result.Usage.PromptTokens,
                completionTokens: result.Usage.CompletionTokens,
                costUsd: result.Cost.TotalCost,
                latencyMs: latencyMs,
                success: true,
                isFreeModel: result.Cost.TotalCost == 0,
                sessionId: null);

            // 6. Usage aggregation service
            if (_openRouterUsageService != null && result.Cost.TotalCost > 0)
                _ = _openRouterUsageService.RecordRequestCostAsync(result.Cost.TotalCost, ct);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to log LLM cost");
        }
    }

    /// <inheritdoc/>
    public async Task LogFailureAsync(
        string? errorMessage,
        LlmUserContext userContext,
        long latencyMs,
        RequestSource source,
        CancellationToken ct = default)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var costLogRepo = scope.ServiceProvider.GetRequiredService<ILlmCostLogRepository>();
                await costLogRepo.LogCostAsync(
                    userContext.UserId,
                    userContext.RoleName ?? "Anonymous",
                    LlmCostCalculation.Empty,
                    endpoint: "completion",
                    success: false,
                    errorMessage: errorMessage,
                    latencyMs: (int)latencyMs,
                    ipAddress: null,
                    userAgent: null,
                    source: source,
                    cancellationToken: ct).ConfigureAwait(false);
            }

            _openRouterFileLogger?.LogRequest(
                requestId: Guid.NewGuid().ToString(),
                model: string.Empty,
                provider: string.Empty,
                source: source.ToString(),
                userId: userContext.UserId,
                promptTokens: 0,
                completionTokens: 0,
                costUsd: 0,
                latencyMs: latencyMs,
                success: false,
                isFreeModel: false,
                sessionId: null,
                errorMessage: errorMessage);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to log LLM error cost");
        }
    }

    private async Task UpdateModelUsageStatsAsync(LlmCompletionResult result, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAiModelConfigurationRepository>();

            var modelConfig = await repository.GetByModelIdAsync(result.Cost.ModelId, ct).ConfigureAwait(false);
            if (modelConfig != null)
            {
                var inputTokens = result.Usage.PromptTokens;
                var outputTokens = result.Usage.CompletionTokens;
                var totalCost = result.Cost.InputCost + result.Cost.OutputCost;

                modelConfig.TrackUsage(inputTokens, outputTokens, totalCost);
                await repository.UpdateAsync(modelConfig, ct).ConfigureAwait(false);

                _logger.LogDebug(
                    "Updated usage stats for model {ModelId}: +{Tokens} tokens, +${Cost:F6}",
                    result.Cost.ModelId, inputTokens + outputTokens, totalCost);
            }
            else
            {
                _logger.LogWarning("Model {ModelId} not found in DB for usage stats update", result.Cost.ModelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update usage stats for model {ModelId}", result.Cost.ModelId);
        }
    }
}
