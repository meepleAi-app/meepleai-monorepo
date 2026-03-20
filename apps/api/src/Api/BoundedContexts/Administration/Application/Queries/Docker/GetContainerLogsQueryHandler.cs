using Api.BoundedContexts.Administration.Application.Queries.Docker;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Docker;

internal sealed class GetContainerLogsQueryHandler
    : IRequestHandler<GetContainerLogsQuery, ContainerLogsDto>
{
    private readonly IDockerProxyService _dockerProxy;

    public GetContainerLogsQueryHandler(IDockerProxyService dockerProxy)
    {
        _dockerProxy = dockerProxy ?? throw new ArgumentNullException(nameof(dockerProxy));
    }

    public async Task<ContainerLogsDto> Handle(
        GetContainerLogsQuery request, CancellationToken cancellationToken)
    {
        return await _dockerProxy.GetContainerLogsAsync(
            request.ContainerId, request.TailLines, cancellationToken).ConfigureAwait(false);
    }
}
