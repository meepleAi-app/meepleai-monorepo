using Api.BoundedContexts.Testing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-8) — Handler for
/// <see cref="SeedTestSessionCommand"/>. Persists a GameNightSessionEntity
/// stamped with explicit <c>TestRunId</c> column for cleanup scope.
/// </summary>
internal sealed class SeedTestSessionCommandHandler
    : IRequestHandler<SeedTestSessionCommand, SeedTestSessionResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<SeedTestSessionCommandHandler> _logger;

    public SeedTestSessionCommandHandler(
        MeepleAiDbContext db,
        ILogger<SeedTestSessionCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedTestSessionResponse> Handle(
        SeedTestSessionCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var gameNight = await _db.GameNightEvents
            .Include(g => g.Sessions)
            .SingleOrDefaultAsync(g => g.Id == request.GameNightId, cancellationToken)
            .ConfigureAwait(false);

        if (gameNight is null)
        {
            throw new NotFoundException("GameNight", request.GameNightId.ToString());
        }

        var sessionId = Guid.NewGuid();
        var nextPlayOrder = gameNight.Sessions.Count + 1;

        // Epic #3188 D4 (post-review FIX 2): GetGameNightLiveQueryHandler now derives IsLive
        // EXCLUSIVELY from the tracking Session (session_tracking_sessions: started_at != null &&
        // finalized_at == null), NOT the link Status. A fixture seeded IsLive:true must therefore ALSO
        // materialize a live tracking Session, otherwise the live view silently renders isLive:false —
        // an untruthful fixture. The tracking Session carries Restrict FKs on user_id (users) and
        // game_id (shared_games), so seed a minimal SharedGame (TestRunId-stamped for cleanup scope) and
        // reuse the night's organizer as the session user. The link's GameId is aligned to that
        // SharedGame so link and tracking Session agree. The non-live (IsLive:false) path is unchanged:
        // no tracking Session, no SharedGame, phantom link GameId — as before.
        var linkGameId = Guid.NewGuid();
        if (request.IsLive)
        {
            var sharedGameId = Guid.NewGuid();
            _db.SharedGames.Add(new SharedGameEntity
            {
                Id = sharedGameId,
                Title = $"E2E Live Game {request.TestRunId[..16]}",
                YearPublished = 2024,
                Description = string.Empty,
                MinPlayers = 1,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                MinAge = 10,
                CreatedBy = gameNight.OrganizerId,
                CreatedAt = DateTime.UtcNow,
                TestRunId = request.TestRunId
            });
            linkGameId = sharedGameId;

            // The canonical liveness row (D4). Id must equal the link's SessionId so
            // GetGameNightLive's `sessionIds.Contains(ts.Id)` join resolves it. Live == StartedAt set
            // + FinalizedAt null (matches Session.IsLive; a live Session's Status is Active).
            _db.SessionTrackingSessions.Add(new SessionEntity
            {
                Id = sessionId,
                UserId = gameNight.OrganizerId,
                GameId = sharedGameId,
                SessionCode = sessionId.ToString("N")[..6].ToUpperInvariant(),
                SessionType = "GameSpecific",
                Status = "Active",
                SessionDate = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow,
                FinalizedAt = null,
                ScoringType = request.ScoreType ?? "Points",
                ScoreData = "{}",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = gameNight.OrganizerId
            });
        }

        var session = new GameNightSessionEntity
        {
            Id = sessionId,
            GameNightEventId = request.GameNightId,
            SessionId = sessionId,
            GameId = linkGameId,
            GameTitle = $"E2E Game {request.TestRunId[..16]}",
            PlayOrder = nextPlayOrder,
            Status = request.IsLive ? "InProgress" : "Pending",
            StartedAt = request.IsLive ? DateTimeOffset.UtcNow : null,
            CompletedAt = null,
            WinnerId = null,
            TestRunId = request.TestRunId
        };

        _db.Set<GameNightSessionEntity>().Add(session);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        _logger.LogInformation(
            "Seeded Session {SessionId} gameNight={GameNightId} isLive={IsLive} testRunId={TestRunId} durationMs={Duration}",
            sessionId, request.GameNightId, request.IsLive, request.TestRunId, stopwatch.ElapsedMilliseconds);

        return new SeedTestSessionResponse(sessionId, request.GameNightId, request.IsLive, request.TestRunId);
    }
}
