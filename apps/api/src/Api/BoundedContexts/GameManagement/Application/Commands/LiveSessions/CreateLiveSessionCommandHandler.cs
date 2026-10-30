using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

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

    public CreateLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork,
        ICompanionSessionService companionSessionService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _companionSessionService = companionSessionService ?? throw new ArgumentNullException(nameof(companionSessionService));
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
            // #4752: the caller (a game night) already owns the tracking Session. Idempotent by that
            // id: a retry, or a go-live after a start that already created the live session, gets the
            // same live session back instead of a second one on the same companion.
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

        SeedInitialPlayers(session, command.InitialPlayers);

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return session.Id;
    }

    /// <summary>
    /// Adds the roster in table order. The palette is the real cap on a table (one colour per active
    /// player, and there are fewer colours than <c>MaxPlayers</c>), so a longer roster is truncated
    /// rather than failing the whole creation; a user already seated is skipped; a display name that
    /// collides (case-insensitively) gets a numeric suffix, because the domain rejects duplicates.
    /// </summary>
    private void SeedInitialPlayers(LiveGameSession session, IReadOnlyList<LiveSessionInitialPlayer>? players)
    {
        if (players is null || players.Count == 0)
            return;

        var colors = Enum.GetValues<PlayerColor>();
        var seatedUsers = new HashSet<Guid>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var colorIndex = 0;

        foreach (var player in players)
        {
            if (colorIndex >= colors.Length)
                break;

            if (player.UserId is { } userId && !seatedUsers.Add(userId))
                continue;

            var name = UniqueDisplayName(player.DisplayName, usedNames);
            session.AddPlayer(player.UserId, name, colors[colorIndex++], _timeProvider);
        }
    }

    private static string UniqueDisplayName(string? requested, HashSet<string> usedNames)
    {
        var baseName = string.IsNullOrWhiteSpace(requested) ? "Giocatore" : requested.Trim();
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
