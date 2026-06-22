using Api.Models;

namespace Api.BoundedContexts.Administration.Domain.Services;

internal interface IProviderProbeService
{
    Task<ProviderProbeResultDto> ProbeAsync(string providerName, Guid actorId, string? expectedModel, CancellationToken cancellationToken);
}
