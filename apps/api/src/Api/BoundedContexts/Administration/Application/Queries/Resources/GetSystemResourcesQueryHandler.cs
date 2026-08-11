using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Handler for <see cref="GetSystemResourcesQuery"/>. Thin pass-through to
/// <see cref="ISystemResourceService"/> (auto-registered by MediatR assembly scan).
/// Issue #3041.
/// </summary>
internal sealed class GetSystemResourcesQueryHandler : IQueryHandler<GetSystemResourcesQuery, SystemResourcesDto>
{
    private readonly ISystemResourceService _service;

    public GetSystemResourcesQueryHandler(ISystemResourceService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public Task<SystemResourcesDto> Handle(GetSystemResourcesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(_service.GetSystemResources());
    }
}
