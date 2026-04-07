using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries.Infrastructure;

internal record GetAiServicesStatusQuery() : IQuery<AiServicesStatusResponse>;

internal class GetAiServicesStatusQueryHandler
    : IQueryHandler<GetAiServicesStatusQuery, AiServicesStatusResponse>
{
    private readonly IInfrastructureHealthService _healthService;
    private readonly IServiceCooldownRegistry _cooldownRegistry;
    private readonly ILogger<GetAiServicesStatusQueryHandler> _logger;

    public GetAiServicesStatusQueryHandler(
        IInfrastructureHealthService healthService,
        IServiceCooldownRegistry cooldownRegistry,
        ILogger<GetAiServicesStatusQueryHandler> logger)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _cooldownRegistry = cooldownRegistry ?? throw new ArgumentNullException(nameof(cooldownRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiServicesStatusResponse> Handle(
        GetAiServicesStatusQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Fetching AI services status for {Count} services",
            ServiceRegistry.AllServiceNames.Count);

        var healthStatuses = await _healthService
            .GetAllServicesHealthAsync(cancellationToken)
            .ConfigureAwait(false);

        var healthMap = healthStatuses.ToDictionary(
            h => h.ServiceName, h => h, StringComparer.Ordinal);

        var services = ServiceRegistry.AllServiceNames.Select(name =>
        {
            var def = ServiceRegistry.Services[name];
            var hasHealth = healthMap.TryGetValue(name, out var health);
            var healthPassed = hasHealth && health!.State == HealthState.Healthy;
            var latencyMs = hasHealth ? health!.ResponseTime.TotalMilliseconds : 0;

            var status = hasHealth
                ? HealthThresholds.DetermineHealth(name, healthPassed, latencyMs, 0)
                : ServiceHealthLevel.Unknown;

            var inCooldown = _cooldownRegistry.IsInCooldown(name, out var remainingSeconds);

            return new AiServiceStatusDto(
                Name: name,
                DisplayName: def.DisplayName,
                Type: def.Type,
                Status: status,
                Uptime: "—",
                AvgLatencyMs: Math.Round(latencyMs, 1),
                ErrorRate24h: 0,
                LastCheckedAt: hasHealth ? health!.CheckedAt : DateTime.MinValue,
                CanRestart: !inCooldown,
                CooldownRemainingSeconds: inCooldown ? remainingSeconds : null);
        }).ToList();

        return new AiServicesStatusResponse(services);
    }
}
