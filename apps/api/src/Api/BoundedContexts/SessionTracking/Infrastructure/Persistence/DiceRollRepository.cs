using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for DiceRoll aggregate.
/// Uses MeepleAiDbContext with persistence entities, maps to/from domain entities.
/// </summary>
public class DiceRollRepository : IDiceRollRepository
{
    private readonly MeepleAiDbContext _context;

    public DiceRollRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<DiceRoll?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.SessionTrackingDiceRolls
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct)
            .ConfigureAwait(false);

        return entity == null ? null : DiceRollMapper.ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiceRoll>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entities = await _context.SessionTrackingDiceRolls
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
        var entities = await _context.SessionTrackingDiceRolls
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
        var entities = await _context.SessionTrackingDiceRolls
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
        await _context.SessionTrackingDiceRolls.AddAsync(entity, ct).ConfigureAwait(false);
    }
}
