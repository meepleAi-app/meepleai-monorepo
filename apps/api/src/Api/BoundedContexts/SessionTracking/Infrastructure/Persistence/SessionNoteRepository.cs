using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for SessionNote persistence operations.
/// </summary>
public class SessionNoteRepository : ISessionNoteRepository
{
    private readonly MeepleAiDbContext _context;

    public SessionNoteRepository(MeepleAiDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<SessionNote?> GetByIdAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SessionNotes
            .FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : SessionNoteMapper.ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionNote>> GetBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.SessionNotes
            .Where(n => n.SessionId == sessionId)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SessionNoteMapper.ToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionNote>> GetByParticipantIdAsync(
        Guid sessionId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.SessionNotes
            .Where(n => n.SessionId == sessionId && n.ParticipantId == participantId)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SessionNoteMapper.ToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionNote>> GetVisibleNotesAsync(
        Guid sessionId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.SessionNotes
            .Where(n => n.SessionId == sessionId &&
                       (n.ParticipantId == requesterId || n.IsRevealed))
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SessionNoteMapper.ToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(SessionNote note, CancellationToken cancellationToken = default)
    {
        var entity = SessionNoteMapper.ToEntity(note);
        await _context.SessionNotes.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpdateAsync(SessionNote note, CancellationToken cancellationToken = default)
    {
        var entity = SessionNoteMapper.ToEntity(note);
        _context.SessionNotes.Update(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
