using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

internal interface IProviderProbeAuditRepository
{
    Task AddAsync(ProviderProbeAuditEntry entry, CancellationToken cancellationToken);
    Task<int> CountInWindowAsync(string providerName, DateTime since, CancellationToken cancellationToken);
    Task<int> CountByActorInWindowAsync(Guid actorId, DateTime since, CancellationToken cancellationToken);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken);
}
