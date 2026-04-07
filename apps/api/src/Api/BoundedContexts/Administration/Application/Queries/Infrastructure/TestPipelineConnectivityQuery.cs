using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries.Infrastructure;

internal record TestPipelineConnectivityQuery() : IQuery<PipelineTestResponse>;

internal class TestPipelineConnectivityQueryHandler
    : IQueryHandler<TestPipelineConnectivityQuery, PipelineTestResponse>
{
    private readonly IInfrastructureHealthService _healthService;
    private readonly ILogger<TestPipelineConnectivityQueryHandler> _logger;

    public TestPipelineConnectivityQueryHandler(
        IInfrastructureHealthService healthService,
        ILogger<TestPipelineConnectivityQueryHandler> logger)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PipelineTestResponse> Handle(
        TestPipelineConnectivityQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hops = new List<PipelineHopDto>();
        var totalLatency = 0.0;
        var success = true;

        foreach (var serviceName in ServiceRegistry.PipelineChain)
        {
            var def = ServiceRegistry.Services[serviceName];

            try
            {
                var health = await _healthService
                    .GetServiceHealthAsync(serviceName, cancellationToken)
                    .ConfigureAwait(false);

                var latencyMs = health.ResponseTime.TotalMilliseconds;
                var isHealthy = health.State == HealthState.Healthy;
                var status = isHealthy ? ServiceHealthLevel.Healthy : ServiceHealthLevel.Down;

                hops.Add(new PipelineHopDto(
                    serviceName, def.DisplayName, status,
                    Math.Round(latencyMs, 1),
                    isHealthy ? null : health.ErrorMessage));

                totalLatency += latencyMs;

                if (!isHealthy)
                {
                    success = false;
                    _logger.LogWarning("Pipeline test failed at {Service}: {Error}",
                        serviceName, health.ErrorMessage);
                    break;
                }
            }
            catch (Exception ex)
            {
                hops.Add(new PipelineHopDto(
                    serviceName, def.DisplayName, ServiceHealthLevel.Down,
                    0, ex.Message));
                success = false;
                _logger.LogError(ex, "Pipeline test exception at {Service}", serviceName);
                break;
            }
        }

        return new PipelineTestResponse(success, hops, Math.Round(totalLatency, 1));
    }
}
