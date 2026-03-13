using Api.BoundedContexts.Administration.Application.Queries.Docker;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.Docker;

internal sealed class GetContainersQueryHandler
    : IRequestHandler<GetContainersQuery, IReadOnlyList<ContainerInfoDto>>
{
    private readonly IDockerProxyService _dockerProxy;

    public GetContainersQueryHandler(IDockerProxyService dockerProxy)
    {
        _dockerProxy = dockerProxy ?? throw new ArgumentNullException(nameof(dockerProxy));
    }

    public async Task<IReadOnlyList<ContainerInfoDto>> Handle(
        GetContainersQuery request, CancellationToken cancellationToken)
    {
        return await _dockerProxy.GetContainersAsync(cancellationToken).ConfigureAwait(false);
    }
}
