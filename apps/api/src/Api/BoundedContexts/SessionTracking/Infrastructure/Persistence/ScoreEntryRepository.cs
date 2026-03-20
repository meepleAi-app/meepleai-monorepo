using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for ScoreEntry entity.
/// Uses MeepleAiDbContext with persistence entities, maps to/from domain entities.
/// </summary>
public class ScoreEntryRepository : RepositoryBase, IScoreEntryRepository
{
    public ScoreEntryRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<IEnumerable<ScoreEntry>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct)
    {
        var entities = await DbContext.SessionTrackingScoreEntries
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(ScoreEntryMapper.ToDomain);
    }

    public async Task<IEnumerable<ScoreEntry>> GetByParticipantAsync(
        Guid sessionId,
        Guid participantId,
        CancellationToken ct)
    {
        var entities = await DbContext.SessionTrackingScoreEntries
            .Where(e => e.SessionId == sessionId && e.ParticipantId == participantId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(ScoreEntryMapper.ToDomain);
    }

    public async Task AddAsync(ScoreEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var entity = ScoreEntryMapper.ToEntity(entry);
        await DbContext.SessionTrackingScoreEntries.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task AddBatchAsync(IEnumerable<ScoreEntry> entries, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryList = entries.ToList();
        if (entryList.Count == 0)
            return;

        var entities = entryList.Select(ScoreEntryMapper.ToEntity).ToList();
        await DbContext.SessionTrackingScoreEntries.AddRangeAsync(entities, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ScoreEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Retrieve existing entity to preserve EF Core tracking
        var existing = await DbContext.SessionTrackingScoreEntries
            .FirstOrDefaultAsync(e => e.Id == entry.Id, ct)
            .ConfigureAwait(false);

        if (existing == null)
            throw new KeyNotFoundException($"ScoreEntry with ID {entry.Id} not found.");

        // Update properties
        existing.ScoreValue = entry.ScoreValue;
        existing.Timestamp = entry.Timestamp;
    }
}
