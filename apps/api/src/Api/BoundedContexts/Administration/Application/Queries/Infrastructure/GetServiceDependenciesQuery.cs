using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Infrastructure;

internal record GetServiceDependenciesQuery(string ServiceName) : IQuery<ServiceDependenciesResponse>;

internal class GetServiceDependenciesQueryHandler
    : IQueryHandler<GetServiceDependenciesQuery, ServiceDependenciesResponse>
{
    private readonly IInfrastructureHealthService _healthService;

    public GetServiceDependenciesQueryHandler(IInfrastructureHealthService healthService)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
    }

    public async Task<ServiceDependenciesResponse> Handle(
        GetServiceDependenciesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var def = ServiceRegistry.Services[request.ServiceName];
        var dependencies = new List<ServiceDependencyDto>();

        foreach (var depName in def.Dependencies)
        {
            var depDef = ServiceRegistry.Services[depName];
            try
            {
                var health = await _healthService
                    .GetServiceHealthAsync(depName, cancellationToken)
                    .ConfigureAwait(false);

                var status = health.State == HealthState.Healthy
                    ? ServiceHealthLevel.Healthy
                    : ServiceHealthLevel.Down;

                dependencies.Add(new ServiceDependencyDto(
                    depName, depDef.DisplayName, status,
                    Math.Round(health.ResponseTime.TotalMilliseconds, 1)));
            }
            catch
            {
                dependencies.Add(new ServiceDependencyDto(
                    depName, depDef.DisplayName, ServiceHealthLevel.Down, 0));
            }
        }

        return new ServiceDependenciesResponse(request.ServiceName, dependencies);
    }
}
