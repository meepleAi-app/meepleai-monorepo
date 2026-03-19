using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class GetTunnelStatusHandler : IQueryHandler<GetTunnelStatusQuery, TunnelStatusResult>
{
    private readonly ISshTunnelClient _tunnelClient;

    public GetTunnelStatusHandler(ISshTunnelClient tunnelClient)
    {
        _tunnelClient = tunnelClient ?? throw new ArgumentNullException(nameof(tunnelClient));
    }

    public Task<TunnelStatusResult> Handle(GetTunnelStatusQuery query, CancellationToken cancellationToken)
        => _tunnelClient.GetStatusAsync(cancellationToken);
}
