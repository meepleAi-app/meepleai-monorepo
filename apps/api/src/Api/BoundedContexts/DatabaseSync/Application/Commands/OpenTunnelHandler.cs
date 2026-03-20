using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DatabaseSync.Application.Commands;

internal class OpenTunnelHandler : ICommandHandler<OpenTunnelCommand, TunnelStatusResult>
{
    private readonly ISshTunnelClient _tunnelClient;

    public OpenTunnelHandler(ISshTunnelClient tunnelClient)
    {
        _tunnelClient = tunnelClient ?? throw new ArgumentNullException(nameof(tunnelClient));
    }

    public Task<TunnelStatusResult> Handle(OpenTunnelCommand command, CancellationToken cancellationToken)
        => _tunnelClient.OpenAsync(cancellationToken);
}
