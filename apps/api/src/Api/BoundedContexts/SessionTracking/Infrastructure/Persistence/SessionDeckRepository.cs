using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for session deck operations.
/// </summary>
public class SessionDeckRepository : ISessionDeckRepository
{
    private readonly MeepleAiDbContext _context;

    public SessionDeckRepository(MeepleAiDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<SessionDeck?> GetByIdAsync(Guid deckId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SessionDecks
            .Include(d => d.Cards)
            .FirstOrDefaultAsync(d => d.Id == deckId, cancellationToken).ConfigureAwait(false);

        return entity == null ? null : SessionDeckMapper.ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<List<SessionDeck>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.SessionDecks
            .Include(d => d.Cards)
            .Where(d => d.SessionId == sessionId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(SessionDeckMapper.ToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(SessionDeck deck, CancellationToken cancellationToken = default)
    {
        var entity = SessionDeckMapper.ToEntity(deck);
        await _context.SessionDecks.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpdateAsync(SessionDeck deck, CancellationToken cancellationToken = default)
    {
        var entity = SessionDeckMapper.ToEntity(deck);

        // Check if entity exists
        var existingEntity = _context.SessionDecks.Local.FirstOrDefault(e => e.Id == entity.Id);
        if (existingEntity != null)
        {
            _context.Entry(existingEntity).CurrentValues.SetValues(entity);

            // Update cards
            existingEntity.Cards.Clear();
            foreach (var card in entity.Cards)
            {
                card.SessionDeckId = entity.Id;
                existingEntity.Cards.Add(card);
            }
        }
        else
        {
            _context.SessionDecks.Update(entity);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
