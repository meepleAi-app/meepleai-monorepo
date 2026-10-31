using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles live session creation.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class CreateLiveSessionCommandHandler : ICommandHandler<CreateLiveSessionCommand, Guid>
{
    // The server compares display names after Trim().ToLowerInvariant() (LiveGameSession.AddPlayer)
    // and the entity caps a name at 100 characters; the roster seeding must agree with both.
    private const int MaxDisplayNameLength = 100;

    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanionSessionService _companionSessionService;
    private readonly IGameNightEventRepository _gameNightRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateLiveSessionCommandHandler> _logger;

    public CreateLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork,
        ICompanionSessionService companionSessionService,
        IGameNightEventRepository gameNightRepository,
        IMediator mediator,
        ILogger<CreateLiveSessionCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _companionSessionService = companionSessionService ?? throw new ArgumentNullException(nameof(companionSessionService));
        _gameNightRepository = gameNightRepository ?? throw new ArgumentNullException(nameof(gameNightRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(CreateLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        SessionScoringConfig scoringConfig;
        if (command.ScoringDimensions != null && command.ScoringDimensions.Count > 0)
        {
            scoringConfig = new SessionScoringConfig(
                command.ScoringDimensions,
                command.DimensionUnits);
        }
        else
        {
            scoringConfig = SessionScoringConfig.CreateDefault();
        }

        Guid? trackingSessionId = null;
        if (command.ExistingTrackingSessionId is { } existingTrackingSessionId)
        {
            // #4752: defence in depth. Only the two night handlers pass this, and each has already checked
            // that the caller is the organizer — but adopting a tracking Session (or, by the idempotency
            // below, being handed the live game of ANY tracking id) must not rest on every future caller
            // remembering that check.
            await EnsureCallerMayAdoptAsync(existingTrackingSessionId, command.UserId, cancellationToken)
                .ConfigureAwait(false);

            // The caller (a game night) already owns the tracking Session. Idempotent by that id: a retry,
            // or a go-live after a start that already created the live session, gets the same live session
            // back instead of a second one on the same companion.
            var existing = await _sessionRepository
                .GetByTrackingSessionIdAsync(existingTrackingSessionId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
                return existing.Id;

            trackingSessionId = existingTrackingSessionId;
        }
        else if (command.GameId.HasValue)
        {
            // ADR-083 SP0: create the SessionTracking.Session companion at-creation (Saga).
            // Add-only (no SaveChanges here): the single SaveChangesAsync below commits the
            // companion and the LiveGameSession atomically in one EF transaction — a companion
            // failure rolls back the LiveGameSession, so no orphan is ever persisted.
            trackingSessionId = await _companionSessionService
                .CreateCompanionAsync(command.UserId, command.GameId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            command.UserId,
            command.GameName,
            _timeProvider,
            command.GameId,
            command.Visibility,
            command.GroupId,
            scoringConfig,
            command.AgentMode,
            trackingSessionId: trackingSessionId);

        SeedInitialPlayers(session, command.InitialPlayers, trackingSessionId);

        // The start needs a player: with an empty roster the game stays Created, which is all the domain
        // allows. The caller's tier and role are read BEFORE anything is staged, so an unknown caller
        // fails with nothing written.
        (UserTier Tier, Role Role)? startAs = command.StartImmediately && session.HasPlayers
            ? await ResolveStarterAsync(command.UserId, cancellationToken).ConfigureAwait(false)
            : null;
        if (command.StartImmediately && startAs is null)
        {
            _logger.LogInformation(
                "Live game {LiveSessionId} (tracking session {TrackingSessionId}) has no players and stays Created",
                session.Id, trackingSessionId);
        }

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (startAs is { } starter)
        {
            // THE start command, not a copy of its rules (quota by tier and role, creator-only guard,
            // correlated GameSession): it reads the live game back from the database, hence the save above.
            await _mediator.Send(
                new StartLiveSessionCommand(session.Id, command.UserId, starter.Tier, starter.Role),
                cancellationToken).ConfigureAwait(false);
        }

        return session.Id;
    }

    private async Task<(UserTier Tier, Role Role)> ResolveStarterAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new GetUserByIdQuery(userId), cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", userId.ToString());

        return (UserTier.Parse(user.Tier), Role.Parse(user.Role));
    }

    /// <summary>
    /// The tracking Session to adopt must be the caller's own (the night start creates it a moment earlier,
    /// in the same transaction) or a sitting of a night the caller organizes (a go-live of a draft that
    /// someone else planned). Anything else is refused: another user's tracking Session is not theirs to
    /// take, and neither is the live game that hangs off it.
    /// </summary>
    private async Task EnsureCallerMayAdoptAsync(
        Guid trackingSessionId, Guid userId, CancellationToken cancellationToken)
    {
        if (await _companionSessionService.IsOwnedByAsync(trackingSessionId, userId, cancellationToken)
                .ConfigureAwait(false))
            return;

        var night = await _gameNightRepository.GetByLinkedSessionIdAsync(trackingSessionId, cancellationToken)
            .ConfigureAwait(false);
        if (night is not null && night.OrganizerId == userId)
            return;

        throw new ForbiddenException("Only the owner of a tracking session, or the organizer of its night, can open its live game.");
    }

    /// <summary>
    /// Adds the roster in table order. The palette is the real cap on a table (one colour per active
    /// player, and there are fewer colours than <c>MaxPlayers</c>), so a longer roster is truncated
    /// rather than failing the whole creation — and says so in the log, with the counts and no names; a
    /// user already seated is skipped; a display name that collides (case-insensitively) gets a numeric
    /// suffix, because the domain rejects duplicates.
    /// </summary>
    private void SeedInitialPlayers(
        LiveGameSession session, IReadOnlyList<LiveSessionInitialPlayer>? players, Guid? trackingSessionId)
    {
        if (players is null || players.Count == 0)
            return;

        var colors = Enum.GetValues<PlayerColor>();
        var seatedUsers = new HashSet<Guid>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var colorIndex = 0;
        var excluded = 0;

        foreach (var player in players)
        {
            if (colorIndex >= colors.Length)
            {
                excluded++;
                continue;
            }

            if (player.UserId is { } userId && !seatedUsers.Add(userId))
                continue;

            var name = UniqueDisplayName(player.DisplayName, usedNames);
            session.AddPlayer(player.UserId, name, colors[colorIndex++], _timeProvider);
        }

        if (excluded > 0)
        {
            _logger.LogWarning(
                "Live game {LiveSessionId} (tracking session {TrackingSessionId}): {Requested} players asked, {Seated} seated, {Excluded} left out for want of a colour",
                session.Id, trackingSessionId, players.Count, colorIndex, excluded);
        }
    }

    private static string UniqueDisplayName(string? requested, HashSet<string> usedNames)
    {
        var baseName = RosterDisplayNames.Resolve(requested, RosterDisplayNames.Player).Trim();
        var candidate = Truncate(baseName, MaxDisplayNameLength);
        var suffix = 2;

        while (!usedNames.Add(candidate.ToLowerInvariant()))
        {
            var tail = $" ({suffix++})";
            candidate = Truncate(baseName, MaxDisplayNameLength - tail.Length) + tail;
        }

        return candidate;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd();
}
