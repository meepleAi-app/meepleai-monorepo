using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for infrastructure health queries.
/// Issue #891: Aggregates health checks from all infrastructure services.
/// </summary>
public class GetInfrastructureHealthQueryHandler : IRequestHandler<GetInfrastructureHealthQuery, InfrastructureHealthResponse>
{
    private readonly IInfrastructureHealthService _healthService;
    private readonly ILogger<GetInfrastructureHealthQueryHandler> _logger;

    public GetInfrastructureHealthQueryHandler(
        IInfrastructureHealthService healthService,
        ILogger<GetInfrastructureHealthQueryHandler> logger)
    {
        _healthService = healthService;
        _logger = logger;
    }

    public async Task<InfrastructureHealthResponse> Handle(
        GetInfrastructureHealthQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting infrastructure health status. ServiceName filter: {ServiceName}",
            request.ServiceName ?? "all");

        try
        {
            var overall = await _healthService.GetOverallHealthAsync(cancellationToken).ConfigureAwait(false);

            var services = request.ServiceName is not null
                ? new[] { await _healthService.GetServiceHealthAsync(request.ServiceName, cancellationToken).ConfigureAwait(false) }
                : await _healthService.GetAllServicesHealthAsync(cancellationToken).ConfigureAwait(false);

            var serviceDtos = services.Select(s => new ServiceHealthDto(
                s.ServiceName,
                s.State.ToString(),
                s.ErrorMessage,
                s.CheckedAt,
                s.ResponseTime.TotalMilliseconds
            )).ToList();

            return new InfrastructureHealthResponse(overall, serviceDtos);
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // HANDLER PATTERN: Log health check failures before propagating.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get infrastructure health status");
            throw;
        }
#pragma warning restore S2139
    }
}
