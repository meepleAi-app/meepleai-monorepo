using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
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

        // Session Flow v2.1 — T4: Resolve or create the GameNight envelope.
        var (nightEntity, gameNightWasCreated) = await ResolveGameNightAsync(
            request, cancellationToken).ConfigureAwait(false);

        var sessionType = Enum.Parse<SessionType>(request.SessionType);

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
                request.SessionDate);

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

            try
            {
                await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);

                // Session Flow v2.1 — T4: Link session to the GameNight envelope.
                var gameTitle = await _db.SharedGames
                    .AsNoTracking()
                    .Where(g => g.Id == request.GameId)
                    .Select(g => g.Title)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false) ?? "Unknown Game";

                // I2 fix: enforce max 5 sessions per GameNight (domain invariant bypass guard)
                var existingSessionCount = await _db.GameNightSessions
                    .CountAsync(gns => gns.GameNightEventId == nightEntity.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (existingSessionCount >= 5)
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
                    Status = GameNightSessionStatus.InProgress.ToString(),
                    StartedAt = _timeProvider.GetUtcNow()
                };

                // Session Flow v2.1 — T5 fix: when nightEntity is loaded (Unchanged) and the
                // child link has a non-default Guid PK, EF identity-resolution would otherwise
                // mark the new linkEntity as Modified instead of Added (because GameNightSessionEntity.Id
                // is configured ValueGeneratedOnAdd). Adding via the DbSet forces Added state and
                // also keeps the in-memory navigation collection consistent for subsequent code paths.
                await _db.GameNightSessions.AddAsync(linkEntity, cancellationToken).ConfigureAwait(false);
                nightEntity.Sessions.Add(linkEntity);

                // Session Flow v2.1 — T4: Emit diary events.
                var diaryEntities = new List<Api.Infrastructure.Entities.SessionTracking.SessionEventEntity>();

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
                    GameNightEventId: nightEntity.Id,
                    GameNightWasCreated: gameNightWasCreated,
                    AgentDefinitionId: agentDefinitionId,
                    ToolkitId: toolkitId);
            }
            catch (InvalidOperationException ex) when (attempt < maxRetries - 1)
            {
                _logger.LogDebug(ex, "Session code collision on attempt {Attempt}, retrying", attempt + 1);
            }
        }

        throw new ConflictException("Unable to generate unique session code after retries");
    }

    /// <summary>
    /// Session Flow v2.1 — T4.
    /// Resolves an existing InProgress GameNightEvent (attaching the requested game to it)
    /// or creates a new ad-hoc night envelope. Enforces the invariant that at most one Active
    /// session may live inside a GameNightEvent at any given time.
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

            if (!string.Equals(existing.Status, nameof(GameNightStatus.InProgress), StringComparison.Ordinal))
            {
                throw new ConflictException(
                    $"Cannot attach session to GameNight {existing.Id}: status is {existing.Status}, expected InProgress.");
            }

            // Invariant: at most one Active session per GameNight.
            // Resolve by joining the link rows (game_night_sessions) with the actual
            // SessionTracking sessions and checking their Status column.
            var activeSessionCount = await _db.GameNightSessions
                .Where(gns => gns.GameNightEventId == existing.Id)
                .Join(
                    _db.SessionTrackingSessions,
                    gns => gns.SessionId,
                    s => s.Id,
                    (gns, s) => s.Status)
                .CountAsync(status => status == nameof(SessionStatus.Active), cancellationToken)
                .ConfigureAwait(false);

            if (activeSessionCount > 0)
            {
                throw new ConflictException(
                    $"GameNight {existing.Id} already has an active session. " +
                    "Pause or finalize it before starting another game.");
            }

            // Attach new game to the existing in-progress night (domain rule).
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
        var title = $"Serata del {_timeProvider.GetUtcNow().UtcDateTime:yyyy-MM-dd HH:mm}";
        var now = _timeProvider.GetUtcNow();
        var newEntity = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = request.UserId,
            Title = title,
            ScheduledAt = now,
            GameIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { request.GameId }),
            Status = nameof(GameNightStatus.InProgress),
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
