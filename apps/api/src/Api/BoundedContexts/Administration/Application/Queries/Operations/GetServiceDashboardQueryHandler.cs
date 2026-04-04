using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Operations;
using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Operations;

/// <summary>
/// Handler for enhanced service dashboard query.
/// Issue #132: Enriches health check data with categories, uptime estimates, and trend indicators.
/// </summary>
internal class GetServiceDashboardQueryHandler : IRequestHandler<GetServiceDashboardQuery, EnhancedServiceDashboardDto>
{
    private readonly IInfrastructureDetailsService _detailsService;
    private readonly ILogger<GetServiceDashboardQueryHandler> _logger;

    /// <summary>
    /// Maps service names to their display categories.
    /// </summary>
    private static readonly Dictionary<string, string> ServiceCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["postgres"] = "Core Infrastructure",
        ["redis"] = "Core Infrastructure",
        ["qdrant"] = "Core Infrastructure",
        ["qdrant-collection"] = "Core Infrastructure",
        ["embedding"] = "AI Services",
        ["n8n"] = "External APIs",
    };

    /// <summary>
    /// Friendly display names for services.
    /// </summary>
    private static readonly Dictionary<string, string> ServiceDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["postgres"] = "PostgreSQL",
        ["redis"] = "Redis",
        ["qdrant"] = "Qdrant",
        ["qdrant-collection"] = "Qdrant Collection",
        ["embedding"] = "Embedding Service",
        ["n8n"] = "n8n Workflows",
    };

    public GetServiceDashboardQueryHandler(
        IInfrastructureDetailsService detailsService,
        ILogger<GetServiceDashboardQueryHandler> logger)
    {
        _detailsService = detailsService ?? throw new ArgumentNullException(nameof(detailsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnhancedServiceDashboardDto> Handle(
        GetServiceDashboardQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _logger.LogInformation("Handling GetServiceDashboardQuery");

        try
        {
            // Fetch health and metrics in parallel
            var detailsTask = _detailsService.GetDetailsAsync(cancellationToken);
            var details = await detailsTask.ConfigureAwait(false);

            var overall = new OverallHealthStatusDto(
                State: details.Overall.State.ToString(),
                TotalServices: details.Overall.TotalServices,
                HealthyServices: details.Overall.HealthyServices,
                DegradedServices: details.Overall.DegradedServices,
                UnhealthyServices: details.Overall.UnhealthyServices,
                CheckedAt: details.Overall.CheckedAt
            );

            var enhancedServices = details.Services.Select(s => new EnhancedServiceHealthDto(
                ServiceName: GetDisplayName(s.ServiceName),
                State: s.State.ToString(),
                ErrorMessage: s.ErrorMessage,
                CheckedAt: s.CheckedAt,
                ResponseTimeMs: s.ResponseTime.TotalMilliseconds,
                Category: GetCategory(s.ServiceName),
                UptimePercent24h: EstimateUptime(s.State),
                ResponseTimeTrend: "stable",
                PreviousResponseTimeMs: null,
                LastIncidentAt: s.State != HealthState.Healthy ? s.CheckedAt : null
            )).ToList();

            var prometheusMetrics = new PrometheusMetricsDto(
                ApiRequestsLast24h: details.Metrics.ApiRequestsLast24h,
                AvgLatencyMs: details.Metrics.AvgLatencyMs,
                ErrorRate: details.Metrics.ErrorRate,
                LlmCostLast24h: details.Metrics.LlmCostLast24h
            );

            _logger.LogInformation(
                "Service dashboard retrieved. Overall: {State}, Services: {Count}",
                overall.State,
                enhancedServices.Count);

            return new EnhancedServiceDashboardDto(overall, enhancedServices, prometheusMetrics);
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // HANDLER PATTERN: Log dashboard query failures before propagating.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle GetServiceDashboardQuery");
            throw;
        }
#pragma warning restore S2139
    }

    private static string GetCategory(string serviceName)
    {
        return ServiceCategoryMap.TryGetValue(serviceName, out var category)
            ? category
            : "External APIs";
    }

    private static string GetDisplayName(string serviceName)
    {
        return ServiceDisplayNames.TryGetValue(serviceName, out var displayName)
            ? displayName
            : serviceName;
    }

    /// <summary>
    /// Estimates uptime percentage based on current health state.
    /// Without historical data, healthy services are assumed 99.9%+.
    /// </summary>
    private static double EstimateUptime(HealthState state) => state switch
    {
        HealthState.Healthy => 99.9,
        HealthState.Degraded => 97.0,
        HealthState.Unhealthy => 85.0,
        _ => 90.0
    };
}
