using Api.BoundedContexts.DatabaseSync.Domain.Models;

namespace Api.BoundedContexts.DatabaseSync.Domain.Interfaces;

internal interface ISshTunnelClient
{
    Task<TunnelStatusResult> GetStatusAsync(CancellationToken ct = default);
    Task<TunnelStatusResult> OpenAsync(CancellationToken ct = default);
    Task<TunnelStatusResult> CloseAsync(CancellationToken ct = default);
}
