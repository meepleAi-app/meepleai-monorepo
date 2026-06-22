using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

internal sealed class ProviderProbeAuditRepository : IProviderProbeAuditRepository
{
    private readonly MeepleAiDbContext _db;

    public ProviderProbeAuditRepository(MeepleAiDbContext db) => _db = db;

    public async Task AddAsync(ProviderProbeAuditEntry entry, CancellationToken cancellationToken)
    {
        _db.ProviderProbeAuditEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountInWindowAsync(string providerName, DateTime since, CancellationToken cancellationToken)
        => _db.ProviderProbeAuditEntries.CountAsync(e => e.ProviderName == providerName && e.ProbedAt >= since, cancellationToken);

    public Task<int> CountByActorInWindowAsync(Guid actorId, DateTime since, CancellationToken cancellationToken)
        => _db.ProviderProbeAuditEntries.CountAsync(e => e.ActorId == actorId && e.ProbedAt >= since, cancellationToken);

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken)
        => await _db.ProviderProbeAuditEntries.Where(e => e.ProbedAt < cutoff).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
}
