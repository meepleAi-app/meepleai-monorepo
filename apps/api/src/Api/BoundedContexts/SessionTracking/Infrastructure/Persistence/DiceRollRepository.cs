using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for DiceRoll aggregate.
/// Uses MeepleAiDbContext with persistence entities, maps to/from domain entities.
/// </summary>
public class DiceRollRepository : RepositoryBase, IDiceRollRepository
{
    public DiceRollRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    /// <inheritdoc />
    public async Task<DiceRoll?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.SessionTrackingDiceRolls
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct)
            .ConfigureAwait(false);

        return entity == null ? null : DiceRollMapper.ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiceRoll>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entities = await DbContext.SessionTrackingDiceRolls
            .AsNoTracking()
            .Where(d => d.SessionId == sessionId)
            .OrderByDescending(d => d.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(DiceRollMapper.ToDomain);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiceRoll>> GetRecentBySessionIdAsync(Guid sessionId, int limit = 20, CancellationToken ct = default)
    {
        var entities = await DbContext.SessionTrackingDiceRolls
            .AsNoTracking()
            .Where(d => d.SessionId == sessionId)
            .OrderByDescending(d => d.Timestamp)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(DiceRollMapper.ToDomain);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiceRoll>> GetByParticipantAsync(Guid sessionId, Guid participantId, CancellationToken ct = default)
    {
        var entities = await DbContext.SessionTrackingDiceRolls
            .AsNoTracking()
            .Where(d => d.SessionId == sessionId && d.ParticipantId == participantId)
            .OrderByDescending(d => d.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(DiceRollMapper.ToDomain);
    }

    /// <inheritdoc />
    public async Task AddAsync(DiceRoll diceRoll, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(diceRoll);

        var entity = DiceRollMapper.ToEntity(diceRoll);
        await DbContext.SessionTrackingDiceRolls.AddAsync(entity, ct).ConfigureAwait(false);
    }
}
