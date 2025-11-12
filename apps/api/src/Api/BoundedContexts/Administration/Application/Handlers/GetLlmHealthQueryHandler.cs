using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetLlmHealthQuery.
/// ISSUE-962 (BGAI-020): Provides real-time LLM provider health monitoring
/// </summary>
public class GetLlmHealthQueryHandler : IQueryHandler<GetLlmHealthQuery, LlmHealthStatusDto>
{
    private readonly ProviderHealthCheckService _healthCheckService;
    private readonly HybridLlmService _hybridLlmService;

    public GetLlmHealthQueryHandler(
        ProviderHealthCheckService healthCheckService,
        HybridLlmService hybridLlmService)
    {
        _healthCheckService = healthCheckService;
        _hybridLlmService = hybridLlmService;
    }

    public Task<LlmHealthStatusDto> Handle(GetLlmHealthQuery query, CancellationToken cancellationToken)
    {
        // Get all provider health statuses
        var allHealth = _healthCheckService.GetAllProviderHealth();

        // Get monitoring status (circuit breaker + latency)
        var monitoringStatus = _hybridLlmService.GetMonitoringStatus();

        // Build provider DTOs
        var providers = new Dictionary<string, ProviderHealthDto>();

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
