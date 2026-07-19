using Api.BoundedContexts.Administration.Domain.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query returning the raw health status of all monitored infrastructure services.
/// Unlike <see cref="GetInfrastructureHealthQuery"/> (which returns a transformed DTO with a
/// string State), this preserves the domain <see cref="ServiceHealthStatus"/> records — including
/// the <c>HealthState</c> enum — for consumers that need the full detail, e.g. the RAG pipeline
/// health endpoint. Issue #3176 (CQRS conformity: endpoints must use IMediator, not services).
/// </summary>
internal record GetServiceHealthStatusesQuery : IQuery<IReadOnlyCollection<ServiceHealthStatus>>;

internal sealed class GetServiceHealthStatusesQueryHandler
    : IRequestHandler<GetServiceHealthStatusesQuery, IReadOnlyCollection<ServiceHealthStatus>>
{
    private readonly IInfrastructureHealthService _healthService;

    public GetServiceHealthStatusesQueryHandler(IInfrastructureHealthService healthService)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
    }

    public Task<IReadOnlyCollection<ServiceHealthStatus>> Handle(
        GetServiceHealthStatusesQuery request,
        CancellationToken cancellationToken)
        => _healthService.GetAllServicesHealthAsync(cancellationToken);
}
