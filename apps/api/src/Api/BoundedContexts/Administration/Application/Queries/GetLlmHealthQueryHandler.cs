using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetLlmHealthQuery.
/// ISSUE-962 (BGAI-020): Provides real-time LLM provider health monitoring
/// Issue #5487: Uses ICircuitBreakerRegistry directly instead of HybridLlmService.
/// </summary>
internal class GetLlmHealthQueryHandler : IQueryHandler<GetLlmHealthQuery, LlmHealthStatusDto>
{
    private readonly IProviderHealthCheckService _healthCheckService;
    private readonly ICircuitBreakerRegistry _circuitBreakerRegistry;

    public GetLlmHealthQueryHandler(
        IProviderHealthCheckService healthCheckService,
        ICircuitBreakerRegistry circuitBreakerRegistry)
    {
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _circuitBreakerRegistry = circuitBreakerRegistry ?? throw new ArgumentNullException(nameof(circuitBreakerRegistry));
    }

    public Task<LlmHealthStatusDto> Handle(GetLlmHealthQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // Get all provider health statuses
        var allHealth = _healthCheckService.GetAllProviderHealth();

        // Get monitoring status (circuit breaker + latency)
        var monitoringStatus = _circuitBreakerRegistry.GetMonitoringStatus();

        // Build provider DTOs
        var providers = new Dictionary<string, ProviderHealthDto>(StringComparer.Ordinal);

        foreach (var (providerName, health) in allHealth)
        {
            var (circuitState, latencyStats) = monitoringStatus.TryGetValue(providerName, out var status)
                ? status
                : ("unknown", "No data");

            var history = health.GetHistory();
            var successfulChecks = history.Count(h => h.success);
            var failedChecks = history.Count - successfulChecks;

            providers[providerName] = new ProviderHealthDto(
                ProviderName: providerName,
                Status: health.Status.ToString(),
                SuccessfulChecks: successfulChecks,
                FailedChecks: failedChecks,
                TotalChecks: history.Count,
                SuccessRate: health.SuccessRate,
                LastCheckTime: health.LastCheckAt,
                CircuitState: circuitState,
                LatencyStats: latencyStats
            );
        }

        // Generate summary
        var healthyCount = allHealth.Count(h => h.Value.IsAvailable());
        var totalCount = allHealth.Count;
        var summary = $"{healthyCount}/{totalCount} providers healthy";

        return Task.FromResult(new LlmHealthStatusDto(providers, summary));
    }
}
