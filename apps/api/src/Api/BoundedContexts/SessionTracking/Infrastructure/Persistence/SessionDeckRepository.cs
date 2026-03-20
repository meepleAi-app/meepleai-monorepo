using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for session deck operations.
/// </summary>
public class SessionDeckRepository : RepositoryBase, ISessionDeckRepository
{
    public SessionDeckRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    /// <inheritdoc />
    public async Task<SessionDeck?> GetByIdAsync(Guid deckId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.SessionDecks
            .Include(d => d.Cards)
            .FirstOrDefaultAsync(d => d.Id == deckId, cancellationToken).ConfigureAwait(false);

        return entity == null ? null : SessionDeckMapper.ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<List<SessionDeck>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.SessionDecks
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
        await DbContext.SessionDecks.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpdateAsync(SessionDeck deck, CancellationToken cancellationToken = default)
    {
        var entity = SessionDeckMapper.ToEntity(deck);

        // Check if entity exists
        var existingEntity = DbContext.SessionDecks.Local.FirstOrDefault(e => e.Id == entity.Id);
        if (existingEntity != null)
        {
            DbContext.Entry(existingEntity).CurrentValues.SetValues(entity);

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
            DbContext.SessionDecks.Update(entity);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
