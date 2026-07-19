using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.EntityConfigurations.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class CreateSessionCommandHandler : ICommandHandler<CreateSessionCommand, CreateSessionResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionQuotaService _quotaService;
    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateSessionCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IDiaryStreamService _diaryStream;

    public CreateSessionCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ISessionQuotaService quotaService,
        MeepleAiDbContext db,
        IMediator mediator,
        ILogger<CreateSessionCommandHandler> logger,
        TimeProvider timeProvider,
        IDiaryStreamService diaryStream)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _diaryStream = diaryStream ?? throw new ArgumentNullException(nameof(diaryStream));
    }

    public async Task<CreateSessionResult> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check session quota before allowing creation
        await CheckSessionQuotaAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        // Session Flow v2.1 — T4: KB readiness pre-check.
        // #2917: standalone gamebook play passes SkipKbReadinessGate=true — gamebook play does not
        // depend on RAG KB readiness, so it must not 422 on a game whose KB is not indexed.
        if (!request.SkipKbReadinessGate)
        {
            var kbReadiness = await _mediator
                .Send(new GetKbReadinessQuery(request.GameId), cancellationToken)
                .ConfigureAwait(false);

            if (!kbReadiness.IsReady)
            {
                _logger.LogWarning(
                    "KB not ready for game {GameId} (state={State}, ready={Ready}, failed={Failed})",
                    request.GameId, kbReadiness.State, kbReadiness.ReadyPdfCount, kbReadiness.FailedPdfCount);

                throw new UnprocessableEntityException(
                    errorCode: "kb_not_ready",
                    message: $"KB for game {request.GameId} is not ready (state: {kbReadiness.State}).");
            }
        }

        // Session Flow v2.1 — T4: Resolve or create the GameNight envelope.
        // WS1 DEC-3: the game-night orchestrators own the session↔night link on the
        // GameNightEvent aggregate, so they skip the envelope here (no phantom night).
        GameNightEventEntity? nightEntity = null;
        var gameNightWasCreated = false;
        if (!request.SkipGameNightEnvelope)
        {
            (nightEntity, gameNightWasCreated) = await ResolveGameNightAsync(
                request, cancellationToken).ConfigureAwait(false);
        }

        var sessionType = Enum.Parse<SessionType>(request.SessionType);

        // BE-3 #1590: resolve the game title ONCE (invariant across retries) — reused for both
        // the domain event snapshot (session.created) and the GameNight link entity below.
        var gameTitle = await _db.SharedGames
            .AsNoTracking()
            .Where(g => g.Id == request.GameId)
            .Select(g => g.Title)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? "Unknown Game";

        // Create session with retry for unique code
        Session session;
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            // GAP-001 stub: request.StateTier is propagated here from StartGameNightSessionCommand
            // but not yet applied — Session.Create does not accept StateTier until the domain
            // model exposes the property (follow-up issue tracked separately).
            session = Session.Create(
                request.UserId,
                request.GameId,
                sessionType,
                request.Location,
                request.SessionDate,
                request.GamebookCampaignId);

            // Populate participants on the domain aggregate BEFORE persisting so that
            // SessionRepository.AddAsync maps the full graph in a single insert — this keeps
            // the entire Handle method atomic in one SaveChangesAsync call (Session Flow v2.1 — T4).

            // Add additional explicit participants (owner already added by factory).
            foreach (var participantDto in request.Participants.Where(p => !p.IsOwner))
            {
                var participantInfo = ParticipantInfo.Create(
                    participantDto.DisplayName,
                    participantDto.IsOwner,
                    session.Participants.Count + 1);

                session.AddParticipant(participantInfo, participantDto.UserId);
            }

            // Session Flow v2.1 — T4: Append guest names as additional participants.
            if (request.GuestNames is { Count: > 0 })
            {
                foreach (var guestName in request.GuestNames)
                {
                    var guestInfo = ParticipantInfo.Create(
                        displayName: guestName,
                        isOwner: false,
                        joinOrder: session.Participants.Count + 1);

                    session.AddParticipant(guestInfo, userId: null);
                }
            }

            // BE-3 #1590: emit session.created BEFORE AddAsync so the durable domain_event_logs
            // row is collected (SessionRepository.AddAsync calls CollectDomainEvents) and committed
            // atomically with the session + the session_events diary row in one SaveChangesAsync.
            // Timestamp uses _timeProvider (same clock as the diary row) so the AC5.b cross-table
            // proximity assertion holds even under a fake test clock.
            session.AddDomainEvent(new SessionCreatedEvent
            {
                SessionId = session.Id,
                UserId = request.UserId,
                SessionCode = session.SessionCode,
                GameId = request.GameId,
                GameName = gameTitle,
                PlayerCount = session.Participants.Count,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            });

            try
            {
                await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);

                // Session Flow v2.1 — T4: Emit diary events (hoisted so the SSE publish loop
                // below works whether or not the game-night envelope was written).
                var diaryEntities = new List<Api.Infrastructure.Entities.SessionTracking.SessionEventEntity>();

                // WS1 DEC-3: the game-night orchestrators own the session↔night link on the
                // GameNightEvent aggregate, so the envelope (link + game-night diary) is skipped
                // here — otherwise this handler would mint a phantom ad-hoc night double-linking
                // the session (breaking FindByLinkedSessionIdAsync).
                if (!request.SkipGameNightEnvelope && nightEntity != null)
                {
                    // I2 fix + epic #3188 Slice 3 (D6): enforce max 5 sessions per GameNight,
                    // counting ONLY non-terminal links (Pending + InProgress). Terminal links
                    // (Completed / Skipped / Corrupted) do NOT consume budget, so a night that
                    // played five games in sequence can still host a sixth draft. The value stays 5.
                    var pendingStatus = GameNightSessionStatus.Pending.ToString();
                    var inProgressStatus = GameNightSessionStatus.InProgress.ToString();
                    var nonTerminalSessionCount = await _db.GameNightSessions
                        .CountAsync(
                            gns => gns.GameNightEventId == nightEntity.Id
                                && (gns.Status == pendingStatus || gns.Status == inProgressStatus),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (nonTerminalSessionCount >= 5)
                        throw new ConflictException("A game night cannot have more than 5 sessions.");

                    var playOrder = Math.Max(1, nightEntity.Sessions.Count + 1);
                    var linkEntity = new GameNightSessionEntity
                    {
                        Id = Guid.NewGuid(),
                        GameNightEventId = nightEntity.Id,
                        SessionId = session.Id,
                        GameId = request.GameId,
                        GameTitle = gameTitle,
                        PlayOrder = playOrder,
                        // Epic #3188 Slice 3 (D1): a direct create yields a DRAFT — the link is born
                        // Pending with no StartedAt, the tracking Session stays Active (no OpenLiveMode
                        // here), and the night is NOT promoted to live. Going live is a separate explicit
                        // step (POST /api/v1/sessions/{id}/go-live) that promotes Pending → InProgress.
                        Status = GameNightSessionStatus.Pending.ToString(),
                        StartedAt = null
                    };

                    // Session Flow v2.1 — T5 fix: when nightEntity is loaded (Unchanged) and the
                    // child link has a non-default Guid PK, EF identity-resolution would otherwise
                    // mark the new linkEntity as Modified instead of Added (because GameNightSessionEntity.Id
                    // is configured ValueGeneratedOnAdd). Adding via the DbSet forces Added state and
                    // also keeps the in-memory navigation collection consistent for subsequent code paths.
                    await _db.GameNightSessions.AddAsync(linkEntity, cancellationToken).ConfigureAwait(false);
                    nightEntity.Sessions.Add(linkEntity);

                    var sessionCreatedPayload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        gameId = request.GameId,
                        gameNightEventId = nightEntity.Id,
                        gameNightWasCreated
                    });
                    var sessionCreatedDiary = new Api.Infrastructure.Entities.SessionTracking.SessionEventEntity
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        GameNightId = nightEntity.Id,
                        EventType = "session_created",
                        Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                        Payload = sessionCreatedPayload,
                        CreatedBy = request.UserId,
                        Source = "system",
                        IsDeleted = false
                    };
                    _db.SessionEvents.Add(sessionCreatedDiary);
                    diaryEntities.Add(sessionCreatedDiary);

                    if (gameNightWasCreated)
                    {
                        var nightCreatedPayload = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            gameNightEventId = nightEntity.Id,
                            title = nightEntity.Title,
                            isAdHoc = true
                        });
                        var nightCreatedDiary = new Api.Infrastructure.Entities.SessionTracking.SessionEventEntity
                        {
                            Id = Guid.NewGuid(),
                            SessionId = session.Id,
                            GameNightId = nightEntity.Id,
                            EventType = "gamenight_created",
                            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                            Payload = nightCreatedPayload,
                            CreatedBy = request.UserId,
                            Source = "system",
                            IsDeleted = false
                        };
                        _db.SessionEvents.Add(nightCreatedDiary);
                        diaryEntities.Add(nightCreatedDiary);
                    }
                    else
                    {
                        var gameAddedPayload = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            addedGameId = request.GameId
                        });
                        var gameAddedDiary = new Api.Infrastructure.Entities.SessionTracking.SessionEventEntity
                        {
                            Id = Guid.NewGuid(),
                            SessionId = session.Id,
                            GameNightId = nightEntity.Id,
                            EventType = "gamenight_game_added",
                            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                            Payload = gameAddedPayload,
                            CreatedBy = request.UserId,
                            Source = "system",
                            IsDeleted = false
                        };
                        _db.SessionEvents.Add(gameAddedDiary);
                        diaryEntities.Add(gameAddedDiary);
                    }
                }

                // Single atomic save for session + participants + guest additions + night link + diary events.
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // Publish diary events to SSE stream after successful save.
                foreach (var de in diaryEntities)
                {
                    _diaryStream.Publish(de.SessionId, new SessionEventDto(
                        de.Id, de.SessionId, de.GameNightId,
                        de.EventType, de.Timestamp, de.Payload,
                        de.CreatedBy, de.Source));
                }

                // Session Flow v2.1 — T4: Best-effort resolution of AgentDefinition + Toolkit
                // for the response (frontend uses these to compose the Hand view).
                var agentDefinitionId = await TryResolveAgentDefinitionIdAsync(request.GameId, cancellationToken).ConfigureAwait(false);
                var toolkitId = await TryResolveToolkitIdAsync(request.GameId, request.UserId, cancellationToken).ConfigureAwait(false);

                return new CreateSessionResult(
                    SessionId: session.Id,
                    SessionCode: session.SessionCode,
                    Participants: session.Participants.Select(MapParticipant).ToList(),
                    GameNightEventId: nightEntity?.Id ?? Guid.Empty, // WS1 DEC-3: Empty when the envelope is skipped
                    GameNightWasCreated: gameNightWasCreated,
                    AgentDefinitionId: agentDefinitionId,
                    ToolkitId: toolkitId);
            }
            catch (DbUpdateException ex) when (IsGameNightLiveSlotViolation(ex))
            {
                // Defensive belt on the partial-unique ix_game_night_sessions_unique_active index.
                // Epic #3188 Slice 3 (D1): a create now mints a Pending (draft) link, which the
                // partial index (InProgress-only) never touches — so in normal operation this catch
                // is unreachable from the create path (two concurrent creates both coexist as drafts,
                // #19). It is kept only so that, should any path ever emit an InProgress link here, a
                // live-slot violation surfaces as a clean 409 rather than a raw 500. The real max-1-live
                // race is enforced at go-live (Slice 2 — GoLiveSessionCommandHandler).
                throw new ConflictException(
                    "This game night already has a live session in progress. Only one live session per game night is allowed.");
            }
            catch (DbUpdateException ex) when (IsGameNightPlayOrderViolation(ex))
            {
                // Epic #3188 post-review (HIGH): two concurrent draft creates on the SAME existing
                // night each read an identical nightEntity.Sessions.Count snapshot (line above) and
                // compute the SAME PlayOrder, so the 2nd INSERT violates the per-night play-order
                // unique index (IX_game_night_sessions_event_play_order). Invariante #19 makes this a
                // benign ordering race (drafts are meant to coexist), so map it to a retryable 409 —
                // never a raw 500 (project rule #2568).
                //
                // Chosen approach: 409 fallback rather than in-place auto-retry. An auto-retry would
                // have to re-invoke SaveChangesAsync on this same UnitOfWork after a failed save, but
                // MeepleAiDbContext.SaveChangesAsyncCore maps the collected domain events (session.created)
                // into durable domain_event_logs + outbox rows on EVERY call via a NON-destructive
                // PeekEvents() and only drains the collector on success — so a second save would
                // double-insert those rows (duplicate EventId). Safely auto-retrying would require
                // detaching the failed attempt's tracked log/outbox entities and clearing the collector
                // between attempts, i.e. reaching into event-sourcing internals well outside this fix's
                // scope. The 409 is fully client-retryable: the retry re-reads Sessions.Count against the
                // now-committed first draft and gets the next PlayOrder, so a single retry resolves it.
                _logger.LogDebug(
                    ex,
                    "Concurrent same-night PlayOrder collision on create (GameNight={GameNightId}); mapping to 409",
                    nightEntity?.Id);
                throw new ConflictException(
                    "Another session was added to this game night at the same time. Please retry.");
            }
            catch (InvalidOperationException ex) when (attempt < maxRetries - 1)
            {
                _logger.LogDebug(ex, "Session code collision on attempt {Attempt}, retrying", attempt + 1);
            }
        }

        throw new ConflictException("Unable to generate unique session code after retries");
    }

    // Issue #3157 C2a — true iff the DbUpdateException is the partial-unique-index violation on
    // the GameNight live slot (game_night_sessions InProgress per event), not some other constraint.
    private static bool IsGameNightLiveSlotViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg
        && string.Equals(pg.SqlState, Npgsql.PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
        && string.Equals(pg.ConstraintName, GameNightSessionEntityConfiguration.UniqueActiveIndexName, StringComparison.Ordinal);

    // Epic #3188 post-review — true iff the DbUpdateException is the unique-index violation on the
    // per-night play-order index (game_night_sessions (event_id, play_order)), raised when two
    // concurrent draft creates compute the same PlayOrder. Distinct from the live-slot index above.
    private static bool IsGameNightPlayOrderViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg
        && string.Equals(pg.SqlState, Npgsql.PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
        && string.Equals(pg.ConstraintName, GameNightSessionEntityConfiguration.PlayOrderIndexName, StringComparison.Ordinal);

    /// <summary>
    /// Session Flow v2.1 — T4 (revised epic #3188 Slice 3).
    /// Resolves an existing Published/InProgress GameNightEvent (attaching the requested game to it)
    /// or creates a new ad-hoc night envelope born Published. A direct create yields a DRAFT (born
    /// Pending link, Session stays Active/not-live), so multiple drafts may coexist per night (#19):
    /// this no longer enforces a max-1-live guard at create — that invariant lives at go-live (Slice 2)
    /// and the partial-unique ix_game_night_sessions_unique_active index on InProgress links.
    /// </summary>
    private async Task<(GameNightEventEntity NightEntity, bool GameNightWasCreated)> ResolveGameNightAsync(
        CreateSessionCommand request, CancellationToken cancellationToken)
    {
        if (request.GameNightEventId.HasValue)
        {
            var existing = await _db.GameNightEvents
                .Include(e => e.Sessions)
                .FirstOrDefaultAsync(e => e.Id == request.GameNightEventId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (existing == null)
            {
                throw new NotFoundException(
                    $"GameNightEvent {request.GameNightEventId.Value} not found.");
            }

            // Epic #3188 Slice 3 (D1/#19): a draft-holding night is Published (born Published on
            // ad-hoc create) and only flips to InProgress once a session goes live (invariante #15).
            // Accept BOTH so a 2nd DRAFT can attach to a Published night — mirrors the guard in
            // GameNightEvent.AddSession (Published || InProgress). Draft/Cancelled/Completed/Corrupted
            // are rejected as a conflict.
            if (!string.Equals(existing.Status, nameof(GameNightStatus.Published), StringComparison.Ordinal)
                && !string.Equals(existing.Status, nameof(GameNightStatus.InProgress), StringComparison.Ordinal))
            {
                throw new ConflictException(
                    $"Cannot attach session to GameNight {existing.Id}: status is {existing.Status}, expected Published or InProgress.");
            }

            // Epic #3188 Slice 3 (#19): the create-time max-1-live guard is REMOVED. A create now
            // produces a DRAFT (Pending link, Session stays Active/not-live), so multiple drafts may
            // coexist per night (parallel-play retrospectives). Max-1-live is enforced only at go-live
            // (Slice 2 — GameNightEvent.StartSession / EnsureCanStartSession) and by the partial-unique
            // ix_game_night_sessions_unique_active index on InProgress links.

            // Attach new game to the existing night (domain rule).
            // We mutate the GameIdsJson list directly to avoid loading the full aggregate.
            var gameIds = string.IsNullOrEmpty(existing.GameIdsJson)
                ? new List<Guid>()
                : System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(existing.GameIdsJson) ?? new List<Guid>();

            if (!gameIds.Contains(request.GameId))
            {
                gameIds.Add(request.GameId);
                existing.GameIdsJson = System.Text.Json.JsonSerializer.Serialize(gameIds);
                existing.UpdatedAt = _timeProvider.GetUtcNow();
            }

            return (existing, false);
        }

        // Ad-hoc night envelope — build directly at the persistence layer to stay in-scope
        // for a single SaveChanges call.
        // Epic #3188 Slice 3 (D1): born Published (a valid draft-holding envelope), NOT InProgress.
        // The night only flips to InProgress when a session goes live (invariante #15), so a
        // one-click create leaves the night Published holding a single Pending draft.
        var title = $"Serata del {_timeProvider.GetUtcNow().UtcDateTime:yyyy-MM-dd HH:mm}";
        var now = _timeProvider.GetUtcNow();
        var newEntity = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = request.UserId,
            Title = title,
            ScheduledAt = now,
            GameIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { request.GameId }),
            Status = nameof(GameNightStatus.Published),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.GameNightEvents.AddAsync(newEntity, cancellationToken).ConfigureAwait(false);
        return (newEntity, true);
    }

    /// <summary>
    /// Session Flow v2.1 — T4: best-effort resolution of AgentDefinitionId for the response.
    /// Returns null when the game is not configured with an agent.
    /// </summary>
    private async Task<Guid?> TryResolveAgentDefinitionIdAsync(Guid gameId, CancellationToken cancellationToken)
    {
        try
        {
            return await _db.SharedGames
                .AsNoTracking()
                .Where(g => g.Id == gameId)
                .Select(g => g.AgentDefinitionId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Best-effort AgentDefinitionId resolution failed for game {GameId}", gameId);
            return null;
        }
    }

    /// <summary>
    /// Session Flow v2.1 — T4: best-effort resolution of ToolkitId for the response.
    /// Prefers the user's own toolkit over the default (if any).
    /// </summary>
    private async Task<Guid?> TryResolveToolkitIdAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var userToolkit = await _db.Toolkits
                .AsNoTracking()
                .Where(t => t.GameId == gameId && t.OwnerUserId == userId)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (userToolkit.HasValue)
            {
                return userToolkit;
            }

            return await _db.Toolkits
                .AsNoTracking()
                .Where(t => t.GameId == gameId && t.IsDefault)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Best-effort ToolkitId resolution failed for game {GameId}", gameId);
            return null;
        }
    }

    private static ParticipantDto MapParticipant(Participant p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        DisplayName = p.DisplayName,
        IsOwner = p.IsOwner,
        JoinOrder = p.JoinOrder,
        FinalRank = p.FinalRank,
        TotalScore = 0
    };

    /// <summary>
    /// Checks session quota before allowing a new session to be created.
    /// </summary>
    /// <param name="userId">User ID to check quota for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="DomainException">Thrown if user not found</exception>
    /// <exception cref="SessionQuotaExceededException">Thrown if quota exceeded</exception>
    private async Task CheckSessionQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Get user tier and role from database
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Tier, u.Role })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            _logger.LogError("User {UserId} not found during session quota check", userId);
            throw new DomainException($"User with ID {userId} not found");
        }

        var userTier = UserTier.Parse(user.Tier);
        var userRole = Role.Parse(user.Role);

        var quotaResult = await _quotaService.CheckQuotaAsync(
            userId,
            userTier,
            userRole,
            cancellationToken).ConfigureAwait(false);

        if (!quotaResult.IsAllowed)
        {
            _logger.LogWarning(
                "Session quota exceeded for user {UserId} ({Tier}): {CurrentCount}/{MaxAllowed} sessions",
                userId,
                userTier.Value,
                quotaResult.CurrentCount,
                quotaResult.MaxAllowed);

            throw new SessionQuotaExceededException(
                quotaResult.DenialReason ?? "Concurrent session limit reached",
                quotaResult.CurrentCount,
                quotaResult.MaxAllowed);
        }

        _logger.LogDebug(
            "Session quota check passed for user {UserId} ({Tier}): {CurrentCount}/{MaxAllowed} sessions",
            userId,
            userTier.Value,
            quotaResult.CurrentCount,
            quotaResult.MaxAllowed);
    }
}
