using Api.BoundedContexts.DatabaseSync.Domain.Enums;

namespace Api.BoundedContexts.DatabaseSync.Domain.Models;

internal sealed record TunnelStatusResult(
    TunnelState Status,
    int UptimeSeconds,
    string? Message
);
