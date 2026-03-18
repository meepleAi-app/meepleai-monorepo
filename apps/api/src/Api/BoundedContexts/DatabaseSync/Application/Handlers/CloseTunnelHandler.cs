using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class CloseTunnelHandler : ICommandHandler<CloseTunnelCommand, TunnelStatusResult>
{
    private readonly ISshTunnelClient _tunnelClient;

    public CloseTunnelHandler(ISshTunnelClient tunnelClient)
    {
        _tunnelClient = tunnelClient ?? throw new ArgumentNullException(nameof(tunnelClient));
    }

    public Task<TunnelStatusResult> Handle(CloseTunnelCommand command, CancellationToken cancellationToken)
        => _tunnelClient.CloseAsync(cancellationToken);
}
