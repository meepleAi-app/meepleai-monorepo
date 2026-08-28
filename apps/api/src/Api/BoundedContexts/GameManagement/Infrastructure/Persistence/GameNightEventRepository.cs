using System.Linq.Expressions;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of GameNightEvent repository.
/// Maps between domain GameNightEvent aggregate and GameNightEventEntity persistence model.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal class GameNightEventRepository : RepositoryBase, IGameNightEventRepository
{
    private readonly ILogger<GameNightEventRepository> _logger;

    public GameNightEventRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<GameNightEventRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Defensive enum parser: returns the parsed value when valid, otherwise logs an error
    /// and returns the corruption fallback. Used to prevent 500 errors when DB contains
    /// legacy/typo'd status values.
    /// </summary>
    private TEnum ParseEnumSafe<TEnum>(
        string rawValue,
        TEnum corruptedFallback,
        string entityId,
        string fieldName) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(rawValue, ignoreCase: false, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        _logger.LogError(
            "Corrupted {EnumType} value '{RawValue}' for entity {EntityId}.{FieldName}. Mapped to {Fallback}.",
            typeof(TEnum).Name, rawValue, entityId, fieldName, corruptedFallback);

        return corruptedFallback;
    }

    public async Task<GameNightEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .OrderByDescending(e => e.ScheduledAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetUpcomingAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .Where(e => e.Status == nameof(GameNightStatus.Published) && e.ScheduledAt > DateTimeOffset.UtcNow)
            .OrderBy(e => e.ScheduledAt)
            .Take(50)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetCompletedAsync(int limit, CancellationToken cancellationToken = default)
    {
        // F20 #1974: dashboard "Recenti" slot. Sources events whose Status is
        // "Completed" OR a published event whose scheduled date has already
        // passed (the BE does not auto-mark Published → Completed on a
        // scheduler, so we treat past Published events as effectively
        // completed for the dashboard surface). Ordered DESC by ScheduledAt
        // since the entity has no explicit CompletedAt column today. The
        // 50-cap mirrors GetUpcomingAsync; the call-site requests fewer.
        var cap = Math.Clamp(limit, 1, 50);
        var now = DateTimeOffset.UtcNow;

        var entities = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .Where(e => e.Status == nameof(GameNightStatus.Completed)
                || (e.Status == nameof(GameNightStatus.Published) && e.ScheduledAt < now))
            .OrderByDescending(e => e.ScheduledAt)
            .Take(cap)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .Where(e => e.OrganizerId == userId || e.Rsvps.Any(r => r.UserId == userId))
            .OrderByDescending(e => e.ScheduledAt)
            .Take(50)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetEventsNeedingReminderAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        // Issue #3866: the note that used to be here ("no AsNoTracking so the reminder job can
        // persist SentAt via SaveChangesAsync") described something that never happened — the
        // DbContext default is NoTracking (PERF-06), so omitting AsNoTracking() does not track
        // anything, and this method returns MAPPED DOMAIN objects in any case. The job writes the
        // flag with a direct ExecuteUpdate (#2720), which is why it works.
        var entities = await DbContext.GameNightEvents
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .Where(e => e.Status == nameof(GameNightStatus.Published) && e.ScheduledAt >= from && e.ScheduledAt <= to)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(GameNightEvent gameNight, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameNight);
        CollectDomainEvents(gameNight);

        var entity = MapToPersistence(gameNight);
        await DbContext.GameNightEvents.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists mutations to an existing aggregate by RECONCILING its child collections
    /// (<see cref="GameNightRsvpEntity"/>, <see cref="GameNightSessionEntity"/>) rather than
    /// re-attaching the whole graph with <c>DbSet.Update()</c>. Votes are handled by dedicated
    /// methods and are intentionally left untouched here (see the body for the xmin rationale).
    ///
    /// <para>The old detached full-graph <c>.Update()</c> marked every child with a set key as
    /// <c>Modified</c>. A child added since the load (its Id is a fresh <c>Guid.NewGuid()</c>) then
    /// became an <c>UPDATE</c> against a nonexistent row → 0 rows affected →
    /// <see cref="DbUpdateConcurrencyException"/> and a silent rollback (invitees lost;
    /// start-session mis-mapped to a false <c>MAX_LIVE_SESSIONS_EXCEEDED</c> 409). It also collided
    /// with the already-tracked instance on the <c>FindByLinkedSessionIdAsync</c> path (EF identity
    /// conflict) — the same failure the reminder job had to work around (#2720).</para>
    ///
    /// <para>This mirrors <see cref="LiveSessionRepository"/>: the root is attached as
    /// <c>Modified</c> (detached load) or updated in place via <c>SetValues</c> (tracked load) so the
    /// round-tripped <c>xmin</c> concurrency token still drives the <c>WHERE</c> clause (Issue
    /// #2703); each child is then Added / Modified / Deleted against its DB-resident rows.</para>
    /// </summary>
    public async Task UpdateAsync(GameNightEvent gameNight, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameNight);
        CollectDomainEvents(gameNight);

        var snapshot = MapToPersistence(gameNight);

        // Capture child rows before the root snapshot is (possibly) stripped of its collections.
        // NB: Votes are intentionally NOT reconciled here — see the ReconcileChildrenAsync calls below.
        var snapshotRsvps = snapshot.Rsvps.ToList();
        var snapshotSessions = snapshot.Sessions.ToList();

        // ── Root: reconcile against the change tracker so we never attach a second instance ──
        var trackedRoot = DbContext.ChangeTracker.Entries<GameNightEventEntity>()
            .FirstOrDefault(e => e.Entity.Id == gameNight.Id)?.Entity;

        if (trackedRoot == null)
        {
            // Detached load (GetByIdAsync / GetByLinkedSessionIdAsync — 13 of 14 callers): attach the
            // root scalar-only as Modified. EF emits UPDATE ... WHERE id = @id AND xmin = @original
            // because the snapshot carries the round-tripped xmin token.
            snapshot.Rsvps.Clear();
            snapshot.Sessions.Clear();
            snapshot.Votes.Clear();
            DbContext.Entry(snapshot).State = EntityState.Modified;
        }
        else
        {
            // Tracked load (SessionStartedHandler via FindByLinkedSessionIdAsync): copy scalars onto
            // the already-tracked root so EF keeps its loaded OriginalValues (xmin) for the WHERE.
            DbContext.Entry(trackedRoot).CurrentValues.SetValues(snapshot);
        }

        // ── Child collections: Added (new) / Modified (existing) / Deleted (removed) ──
        // Mind the FK asymmetry: Rsvp keys back via EventId, Session via GameNightEventId.
        // RSVPs and Sessions mutate ONLY through this aggregate + UpdateAsync, and the forced-Modified
        // root above always advances xmin, so a concurrent aggregate write loses the xmin race and
        // rolls back before it can wrongly delete a sibling's row — the Delete branch is safe for them.
        // (No domain method removes an RSVP/Session today, so the Delete branch is defensive/unexercised
        // — kept to mirror LiveSessionRepository and to stay correct if such a mutation is added later.)
        await ReconcileChildrenAsync(
            snapshotRsvps, r => r.Id, r => r.EventId == gameNight.Id,
            id => new GameNightRsvpEntity { Id = id, EventId = gameNight.Id },
            cancellationToken).ConfigureAwait(false);

        await ReconcileChildrenAsync(
            snapshotSessions, s => s.Id, s => s.GameNightEventId == gameNight.Id,
            id => new GameNightSessionEntity { Id = id, GameNightEventId = gameNight.Id },
            cancellationToken).ConfigureAwait(false);

        // Votes are deliberately excluded: they are persisted by the dedicated AddVoteAsync /
        // RemoveVoteAsync / SetVotingWinnerAsync paths, which touch a single vote row WITHOUT
        // advancing the root xmin. A concurrently-cast vote is therefore invisible to a stale-snapshot
        // UpdateAsync; reconciling votes here (with a Delete branch) would silently drop it. UpdateAsync
        // leaves vote rows untouched — the vote lifecycle lives entirely on those dedicated methods.
    }

    /// <summary>
    /// Reconciles one child collection of the aggregate against its DB-resident rows: children new
    /// since the load are <c>Added</c>, existing ones <c>Modified</c>, and rows no longer present are
    /// <c>Deleted</c>. This is what keeps a NEW child (Id already assigned via <c>Guid.NewGuid()</c>
    /// in the domain) from being wrongly marked <c>Modified</c> — an UPDATE against a nonexistent row.
    /// </summary>
    private async Task ReconcileChildrenAsync<TChild>(
        IReadOnlyList<TChild> snapshotChildren,
        Expression<Func<TChild, Guid>> idSelector,
        Expression<Func<TChild, bool>> foreignKeyFilter,
        Func<Guid, TChild> deletedStubFactory,
        CancellationToken cancellationToken)
        where TChild : class
    {
        var idOf = idSelector.Compile();

        var existingIds = (await DbContext.Set<TChild>()
            .AsNoTracking()
            .Where(foreignKeyFilter)
            .Select(idSelector)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToHashSet();

        foreach (var child in snapshotChildren)
        {
            var id = idOf(child);
            AttachOrUpdate(child, id, existingIds.Contains(id)
                ? EntityState.Modified
                : EntityState.Added);
        }

        // Delete rows the domain dropped.
        var snapshotIds = snapshotChildren.Select(idOf).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            AttachOrUpdate(deletedStubFactory(removedId), removedId, EntityState.Deleted);
        }
    }

    /// <summary>
    /// Attaches <paramref name="snapshotEntity"/> with the given <paramref name="targetState"/>,
    /// correctly handling the case where an entity with the same key is already tracked (e.g. a child
    /// loaded via <c>Include</c> on the tracked-root path): it updates that entry in place instead of
    /// attaching a duplicate, which would throw an EF identity conflict. Mirrors
    /// <see cref="LiveSessionRepository"/>.AttachOrUpdate.
    /// </summary>
    private void AttachOrUpdate<TChild>(TChild snapshotEntity, Guid id, EntityState targetState)
        where TChild : class
    {
        var existingEntry = DbContext.ChangeTracker.Entries<TChild>()
            .FirstOrDefault(e => e.Property("Id").CurrentValue is Guid g && g == id);

        if (existingEntry != null)
        {
            if (targetState == EntityState.Modified)
            {
                existingEntry.CurrentValues.SetValues(snapshotEntity);
                existingEntry.State = EntityState.Modified;
            }
            else if (targetState == EntityState.Deleted)
            {
                existingEntry.State = EntityState.Deleted;
            }
            // Added is a no-op when the entity is already tracked.
        }
        else
        {
            DbContext.Entry(snapshotEntity).State = targetState;
        }
    }

    public Task DeleteAsync(GameNightEvent gameNight, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameNight);
        var entity = MapToPersistence(gameNight);
        DbContext.GameNightEvents.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameNightEvents
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameNightEvent?> FindByLinkedSessionIdAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Issue #3866: the note that used to be here claimed the entity stays tracked end-to-end.
        // It does not — the DbContext default is NoTracking (PERF-06) and this method returns a
        // MAPPED DOMAIN object anyway. Persistence works because UpdateAsync reconciles the detached
        // aggregate itself (AttachOrUpdate), not because of change detection on this read.
        var entity = await DbContext.GameNightEvents
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .FirstOrDefaultAsync(
                e => e.Sessions.Any(s => s.SessionId == sessionId),
                cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<GameNightEvent?> GetByLinkedSessionIdAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        // AsNoTracking — the go-live orchestrator (GoLiveSessionCommandHandler) mutates the
        // returned aggregate and persists it through the detached branch of UpdateAsync's child
        // reconciliation (mirrors GetByIdAsync feeding StartGameNightSessionCommandHandler). The
        // tracked branch (used by FindByLinkedSessionIdAsync) is equally supported now; this loader
        // simply stays detached so its callers keep the cheaper AsNoTracking read.
        var entity = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .FirstOrDefaultAsync(
                e => e.Sessions.Any(s => s.SessionId == sessionId),
                cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<GameNightEvent?> GetByShareTokenAsync(
        string shareToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shareToken);

        var entity = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)
            .Include(e => e.Votes)
            .FirstOrDefaultAsync(e => e.ShareToken == shareToken && e.IsShared, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddVoteAsync(GameNightVote vote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vote);

        await DbContext.GameNightVotes.AddAsync(new GameNightVoteEntity
        {
            Id = vote.Id,
            EventId = vote.EventId,
            VoterUserId = vote.VoterUserId,
            CandidateGameId = vote.CandidateGameId,
            CreatedAt = vote.CreatedAt
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveVoteAsync(Guid voteId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameNightVotes
            .FirstOrDefaultAsync(v => v.Id == voteId, cancellationToken).ConfigureAwait(false);
        if (entity != null)
        {
            DbContext.GameNightVotes.Remove(entity);
        }
    }

    public async Task SetVotingWinnerAsync(
        Guid eventId, Guid? winnerGameId, CancellationToken cancellationToken = default)
    {
        // Direct column write — bypasses the aggregate's detached full-remap (which would
        // re-Modify every child) and the xmin concurrency token (tie resolution is a single
        // organiser action, not a contended write).
        await DbContext.GameNightEvents
            .Where(e => e.Id == eventId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.VotingWinnerGameId, winnerGameId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static GameNightEventEntity MapToPersistence(GameNightEvent domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var entity = new GameNightEventEntity
        {
            Id = domain.Id,
            OrganizerId = domain.OrganizerId,
            Title = domain.Title,
            Description = domain.Description,
            ScheduledAt = domain.ScheduledAt,
            Location = domain.Location,
            MaxPlayers = domain.MaxPlayers,
            GameIdsJson = JsonSerializer.Serialize(domain.GameIds),
            Status = domain.Status.ToString(),
            Reminder24hSentAt = domain.Reminder24hSentAt,
            Reminder1hSentAt = domain.Reminder1hSentAt,
            RsvpDeadline = domain.RsvpDeadline,
            RsvpClosedAt = domain.RsvpClosedAt,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            VotingWinnerGameId = domain.VotingWinnerGameId,
            ShareToken = domain.ShareToken,
            IsShared = domain.IsShared,
            IsArchived = domain.IsArchived,
            Xmin = domain.Xmin
        };

        foreach (var rsvp in domain.Rsvps)
        {
            entity.Rsvps.Add(new GameNightRsvpEntity
            {
                Id = rsvp.Id,
                EventId = rsvp.EventId,
                UserId = rsvp.UserId,
                Status = rsvp.Status.ToString(),
                RespondedAt = rsvp.RespondedAt,
                CreatedAt = rsvp.CreatedAt
            });
        }

        foreach (var session in domain.Sessions)
        {
            entity.Sessions.Add(new GameNightSessionEntity
            {
                Id = session.Id,
                GameNightEventId = session.GameNightEventId,
                SessionId = session.SessionId,
                GameId = session.GameId,
                GameTitle = session.GameTitle,
                PlayOrder = session.PlayOrder,
                Status = session.Status.ToString(),
                WinnerId = session.WinnerId,
                StartedAt = session.StartedAt,
                CompletedAt = session.CompletedAt
            });
        }

        foreach (var vote in domain.Votes)
        {
            entity.Votes.Add(new GameNightVoteEntity
            {
                Id = vote.Id,
                EventId = vote.EventId,
                VoterUserId = vote.VoterUserId,
                CandidateGameId = vote.CandidateGameId,
                CreatedAt = vote.CreatedAt
            });
        }

        return entity;
    }

    private GameNightEvent MapToDomain(GameNightEventEntity entity)
    {
        var gameIds = string.IsNullOrEmpty(entity.GameIdsJson)
            ? new List<Guid>()
            : JsonSerializer.Deserialize<List<Guid>>(entity.GameIdsJson) ?? [];

        var status = ParseEnumSafe(
            entity.Status,
            GameNightStatus.Corrupted,
            entity.Id.ToString(),
            nameof(entity.Status));

        // Use the internal constructor to reconstitute from persistence
        var evt = new GameNightEvent(
            id: entity.Id,
            organizerId: entity.OrganizerId,
            title: entity.Title,
            scheduledAt: entity.ScheduledAt,
            description: entity.Description,
            location: entity.Location,
            maxPlayers: entity.MaxPlayers,
            gameIds: gameIds);

        // Restore state from persistence via reflection (same pattern as GameNightPlaylistRepository)
        var statusProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.Status));
        statusProp?.SetValue(evt, status);

        var createdAtProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.CreatedAt));
        createdAtProp?.SetValue(evt, entity.CreatedAt);

        var updatedAtProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.UpdatedAt));
        updatedAtProp?.SetValue(evt, entity.UpdatedAt);

        var reminder24hProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.Reminder24hSentAt));
        reminder24hProp?.SetValue(evt, entity.Reminder24hSentAt);

        var reminder1hProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.Reminder1hSentAt));
        reminder1hProp?.SetValue(evt, entity.Reminder1hSentAt);

        var rsvpDeadlineProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.RsvpDeadline));
        rsvpDeadlineProp?.SetValue(evt, entity.RsvpDeadline);

        var rsvpClosedAtProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.RsvpClosedAt));
        rsvpClosedAtProp?.SetValue(evt, entity.RsvpClosedAt);

        // Restore the xmin concurrency token so the detached UpdateAsync emits the
        // WHERE id = @id AND xmin = @original check (Issue #2703).
        evt.SetXmin(entity.Xmin);

        // Restore RSVPs
        var rsvps = entity.Rsvps.Select(r => GameNightRsvp.Reconstitute(
            id: r.Id,
            eventId: r.EventId,
            userId: r.UserId,
            status: ParseEnumSafe(
                r.Status,
                RsvpStatus.Corrupted,
                r.Id.ToString(),
                nameof(r.Status)),
            respondedAt: r.RespondedAt,
            createdAt: r.CreatedAt)).ToList();

        evt.RestoreRsvps(rsvps);

        // Restore Sessions
        if (entity.Sessions?.Count > 0)
        {
            var sessions = entity.Sessions
                .OrderBy(s => s.PlayOrder)
                .Select(s => GameNightSession.Reconstitute(
                    id: s.Id,
                    gameNightEventId: s.GameNightEventId,
                    sessionId: s.SessionId,
                    gameId: s.GameId,
                    gameTitle: s.GameTitle,
                    playOrder: s.PlayOrder,
                    status: ParseEnumSafe(
                        s.Status,
                        GameNightSessionStatus.Corrupted,
                        s.Id.ToString(),
                        nameof(s.Status)),
                    winnerId: s.WinnerId,
                    startedAt: s.StartedAt,
                    completedAt: s.CompletedAt))
                .ToList();

            evt.RestoreSessions(sessions);
        }

        // Restore candidate-game votes + resolved tie-break winner (#2700)
        var votes = entity.Votes
            .Select(v => GameNightVote.Reconstitute(
                id: v.Id,
                eventId: v.EventId,
                voterUserId: v.VoterUserId,
                candidateGameId: v.CandidateGameId,
                createdAt: v.CreatedAt))
            .ToList();
        evt.RestoreVotes(votes);
        evt.RestoreVotingWinner(entity.VotingWinnerGameId);
        evt.RestoreShareState(entity.ShareToken, entity.IsShared, entity.IsArchived);

        // Clear domain events from reconstruction
        evt.ClearDomainEvents();

        return evt;
    }
}
