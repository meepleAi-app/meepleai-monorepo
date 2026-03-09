using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Operations;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.Operations;

/// <summary>
/// Handler for GetServiceMetricsQuery.
/// Issue #3696: Operations - Service Control Panel.
/// Provides uptime, latency, and request count metrics.
/// Note: This is a simplified implementation. Production would integrate with metrics backend (Prometheus, etc.).
/// </summary>
internal sealed class GetServiceMetricsQueryHandler
    : IRequestHandler<GetServiceMetricsQuery, ServiceMetricsResponseDto>
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GetServiceMetricsQueryHandler> _logger;

    public GetServiceMetricsQueryHandler(
        ILogger<GetServiceMetricsQueryHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<ServiceMetricsResponseDto> Handle(
        GetServiceMetricsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var timestamp = _timeProvider.GetUtcNow().UtcDateTime;

        // No monitoring backend connected yet — return empty list
        var services = new List<ServiceMetricsDto>();

        _logger.LogInformation("Service metrics requested — no monitoring backend connected, returning empty list");

        return Task.FromResult(new ServiceMetricsResponseDto(
            Services: services,
            Timestamp: timestamp
        ));
    }
}
