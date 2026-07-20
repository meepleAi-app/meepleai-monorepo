using Api.BoundedContexts.Testing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-3, DEC-B-8) + Issue #1929 Task C Macro 3a
/// (DEC-C-8) + Issue #1929 Task C Macro 4 (DEC-C-10 REVISION) — Handler for
/// <see cref="CleanupTestEntitiesCommand"/>. Cascade-delete by explicit TestRunId
/// column on 8 persistence entities. FK dependency order:
/// UserGameSessions → session_tracking_sessions (epic #3188 FIX 2, scoped via the seeded
/// game_night_sessions links) → GameNightSessions → Rsvps → Invitations → GameNightEvents →
/// UserLibraryEntries → SharedGames → Users.
/// UserGameSession must be deleted BEFORE UserLibraryEntries (FK child).
/// Live tracking Sessions must be deleted before their game_night_sessions join source AND before
/// SharedGames + Users (session_tracking_sessions has Restrict FKs on game_id + user_id).
/// UserLibraryEntry must be deleted before SharedGame (FK constraint);
/// SharedGame must be deleted before User (FK CreatedBy).
/// </summary>
internal sealed class CleanupTestEntitiesCommandHandler
    : IRequestHandler<CleanupTestEntitiesCommand, CleanupTestEntitiesResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<CleanupTestEntitiesCommandHandler> _logger;

    public CleanupTestEntitiesCommandHandler(
        MeepleAiDbContext db,
        ILogger<CleanupTestEntitiesCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CleanupTestEntitiesResponse> Handle(
        CleanupTestEntitiesCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // FK dependency order: child rows first, parent rows last.

        // Issue #1929 Task C Macro 4 (DEC-C-10 REVISION) — UserGameSessions FIRST
        // (FK child of UserLibraryEntries via UserLibraryEntryId).
        var deletedUserGameSessions = await _db.Set<UserGameSessionEntity>()
            .Where(s => s.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // Epic #3188 post-review (FIX 2): SeedTestSessionCommandHandler now materializes a live
        // tracking Session (session_tracking_sessions) for IsLive fixtures so GetGameNightLive reports
        // isLive:true under D4. That row has no TestRunId column of its own, so scope it via the seeded
        // game_night_sessions links (which DO carry TestRunId). It MUST be deleted BEFORE the
        // game_night_sessions delete (the join source) AND before the users + shared_games deletes,
        // because session_tracking_sessions has Restrict FKs on user_id → users and game_id →
        // shared_games. Non-live fixtures seed no tracking Session, so this is a no-op for them.
        var seededTrackedSessionIds = _db.GameNightSessions
            .Where(s => s.TestRunId == request.TestRunId)
            .Select(s => s.SessionId);
        await _db.SessionTrackingSessions
            .Where(ts => seededTrackedSessionIds.Contains(ts.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedSessions = await _db.Set<GameNightSessionEntity>()
            .Where(s => s.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedRsvps = await _db.Set<GameNightRsvpEntity>()
            .Where(r => r.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedInvitations = await _db.Set<GameNightInvitationEntity>()
            .Where(i => i.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedGameNights = await _db.GameNightEvents
            .Where(g => g.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // Issue #1929 Task C Macro 3a (DEC-C-8) — Library catalog cascade.
        // UserLibraryEntries first (FK SharedGameId), then SharedGames (FK CreatedBy → User).
        var deletedLibraryEntries = await _db.UserLibraryEntries
            .Where(e => e.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedSharedGames = await _db.SharedGames
            .Where(g => g.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedUsers = await _db.Users
            .Where(u => u.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        _logger.LogInformation(
            "Cleaned up testRunId={TestRunId} gameNights={GN} sessions={S} invitations={I} rsvps={R} users={U} libraryEntries={LE} sharedGames={SG} userGameSessions={UGS} durationMs={Duration}",
            request.TestRunId, deletedGameNights, deletedSessions, deletedInvitations, deletedRsvps, deletedUsers, deletedLibraryEntries, deletedSharedGames, deletedUserGameSessions, stopwatch.ElapsedMilliseconds);

        return new CleanupTestEntitiesResponse(
            request.TestRunId,
            deletedGameNights,
            deletedSessions,
            deletedInvitations,
            deletedRsvps,
            deletedUsers,
            deletedLibraryEntries,
            deletedSharedGames,
            deletedUserGameSessions,
            stopwatch.ElapsedMilliseconds);
    }
}
