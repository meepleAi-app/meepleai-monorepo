using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for Session aggregate.
/// Uses MeepleAiDbContext with persistence entities, maps to/from domain entities.
/// </summary>
public class SessionRepository : RepositoryBase, ISessionRepository
{
    public SessionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var entity = await DbContext.SessionTrackingSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            .ConfigureAwait(false);

        return entity == null ? null : SessionMapper.ToDomain(entity);
    }

    public async Task<Session?> GetByCodeAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Session code cannot be empty.", nameof(code));

        var normalizedCode = code.ToUpperInvariant();
        var entity = await DbContext.SessionTrackingSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.SessionCode == normalizedCode, ct)
            .ConfigureAwait(false);

        return entity == null ? null : SessionMapper.ToDomain(entity);
    }

    public async Task<Session?> GetByInviteTokenAsync(string inviteToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inviteToken))
            return null;

        var entity = await DbContext.SessionTrackingSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.InviteToken == inviteToken, ct)
            .ConfigureAwait(false);

        return entity == null ? null : SessionMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<Session>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var entities = await DbContext.SessionTrackingSessions
            .Include(s => s.Participants)
            .Where(s => s.UserId == userId && s.Status != SessionStatus.Finalized.ToString())
            .OrderByDescending(s => s.SessionDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(SessionMapper.ToDomain);
    }

    public async Task AddAsync(Session session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Check for session code collision
        var exists = await DbContext.SessionTrackingSessions
            .AnyAsync(s => s.SessionCode == session.SessionCode, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new InvalidOperationException($"Session code {session.SessionCode} already exists.");
        }

        var entity = SessionMapper.ToEntity(session);
        await DbContext.SessionTrackingSessions.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Session session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Retrieve existing entity to preserve EF Core tracking
        var existing = await DbContext.SessionTrackingSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == session.Id, ct)
            .ConfigureAwait(false);

        if (existing == null)
            throw new KeyNotFoundException($"Session with ID {session.Id} not found.");

        // Update scalar properties
        existing.Status = session.Status.ToString();
        existing.Location = session.Location;
        existing.FinalizedAt = session.FinalizedAt;
        existing.IsDeleted = session.IsDeleted;
        existing.DeletedAt = session.DeletedAt;
        existing.UpdatedAt = session.UpdatedAt;
        existing.UpdatedBy = session.UpdatedBy;
        existing.InviteToken = session.InviteToken;
        existing.InviteExpiresAt = session.InviteExpiresAt;

        // Update participants (sync collection)
        existing.Participants.Clear();
        foreach (var participant in session.Participants)
        {
            existing.Participants.Add(ParticipantMapper.ToEntity(participant));
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var session = await GetByIdAsync(id, ct).ConfigureAwait(false);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {id} not found.");

        session.SoftDelete();
        await UpdateAsync(session, ct).ConfigureAwait(false);
    }
}
