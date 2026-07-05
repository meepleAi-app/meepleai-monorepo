using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Aggregate root representing a game night event — a scheduled gathering for board gaming.
/// Supports RSVP management, reminders, and lifecycle transitions (Draft → Published → Completed/Cancelled).
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
#pragma warning disable MA0049
internal sealed class GameNightEvent : AggregateRoot<Guid>
#pragma warning restore MA0049
{
    private readonly List<GameNightRsvp> _rsvps = new();

    public Guid OrganizerId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public string? Location { get; private set; }
    public int? MaxPlayers { get; private set; }
    public List<Guid> GameIds { get; private set; } = [];
    public GameNightStatus Status { get; private set; }
    public DateTimeOffset? Reminder24hSentAt { get; private set; }
    public DateTimeOffset? Reminder1hSentAt { get; private set; }

    // RSVP deadline — ADR-074 (#2383 follow-up)
    /// <summary>
    /// Optional cutoff after which RSVP submission is rejected by the validator.
    /// Set by the organiser via <see cref="SetRsvpDeadline"/>. Per ADR-074 Option C
    /// (lazy validation-time closure check + nullable closed-at timestamp).
    /// </summary>
    public DateTimeOffset? RsvpDeadline { get; private set; }

    /// <summary>
    /// Timestamp at which RSVPs were closed (either by deadline expiry observed at
    /// validation time, by Hangfire cleanup job, or by manual organiser closure).
    /// Once set, no further RSVPs are accepted regardless of <see cref="RsvpDeadline"/>.
    /// </summary>
    public DateTimeOffset? RsvpClosedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public IReadOnlyList<GameNightRsvp> Rsvps => _rsvps.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameNightEvent() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new game night event.
    /// </summary>
    internal GameNightEvent(
        Guid id,
        Guid organizerId,
        string title,
        DateTimeOffset scheduledAt,
        string? description = null,
        string? location = null,
        int? maxPlayers = null,
        List<Guid>? gameIds = null) : base(id)
    {
        if (organizerId == Guid.Empty)
            throw new ArgumentException("OrganizerId cannot be empty", nameof(organizerId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));
        if (title.Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters", nameof(title));
        if (maxPlayers.HasValue && maxPlayers.Value < 2)
            throw new ArgumentException("MaxPlayers must be >= 2", nameof(maxPlayers));

        OrganizerId = organizerId;
        Title = title.Trim();
        Description = description?.Trim();
        ScheduledAt = scheduledAt;
        Location = location?.Trim();
        MaxPlayers = maxPlayers;
        GameIds = gameIds ?? [];
        Status = GameNightStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new game night event.
    /// </summary>
    public static GameNightEvent Create(
        Guid organizerId,
        string title,
        DateTimeOffset scheduledAt,
        string? description = null,
        string? location = null,
        int? maxPlayers = null,
        List<Guid>? gameIds = null)
    {
        return new GameNightEvent(Guid.NewGuid(), organizerId, title, scheduledAt, description, location, maxPlayers, gameIds);
    }

    /// <summary>
    /// Creates an ad-hoc game night that skips the scheduling/RSVP flow.
    /// Used when a user spontaneously starts playing and later adds more games during the same evening.
    /// Status is set directly to InProgress.
    /// </summary>
    public static GameNightEvent CreateAdHoc(Guid organizerId, string title, Guid firstGameId)
    {
        if (firstGameId == Guid.Empty)
            throw new ArgumentException("FirstGameId cannot be empty.", nameof(firstGameId));

        var night = new GameNightEvent(
            id: Guid.NewGuid(),
            organizerId: organizerId,
            title: title,
            scheduledAt: DateTimeOffset.UtcNow,
            description: null,
            location: null,
            maxPlayers: null,
            gameIds: new List<Guid> { firstGameId });

        night.Status = GameNightStatus.InProgress;
        return night;
    }

    /// <summary>
    /// Completes an in-progress ad-hoc night. Transition: InProgress → Completed.
    /// Unlike <see cref="Complete"/> (Published → Completed), this handles the ad-hoc
    /// lifecycle where the night skipped scheduling and went straight to InProgress.
    /// </summary>
    public void CompleteAdHoc()
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot complete a {Status} game night via ad-hoc completion. Only InProgress nights can be completed via this method.");

        Status = GameNightStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds a game to an in-progress ad-hoc night. Idempotent for duplicates.
    /// </summary>
    public void AttachAdditionalGame(Guid gameId)
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot attach game to a {Status} game night. Only InProgress nights accept dynamic games.");

        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));

        if (!GameIds.Contains(gameId))
        {
            GameIds.Add(gameId);
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Updates the game night event details. Cannot update cancelled or completed events.
    /// </summary>
    public void Update(string title, string? description, DateTimeOffset scheduledAt, string? location, int? maxPlayers, List<Guid>? gameIds)
    {
        ThrowIfCorrupted();

        if (Status == GameNightStatus.Cancelled || Status == GameNightStatus.Completed)
            throw new InvalidOperationException($"Cannot update a {Status} game night");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));
        if (title.Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters", nameof(title));
        if (maxPlayers.HasValue && maxPlayers.Value < 2)
            throw new ArgumentException("MaxPlayers must be >= 2", nameof(maxPlayers));

        Title = title.Trim();
        Description = description?.Trim();
        ScheduledAt = scheduledAt;
        Location = location?.Trim();
        MaxPlayers = maxPlayers;
        GameIds = gameIds ?? GameIds;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Publishes the game night and creates RSVP entries for invited users.
    /// Raises a GameNightPublishedEvent.
    /// </summary>
    public void Publish(List<Guid> invitedUserIds)
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.Draft)
            throw new InvalidOperationException("Only draft game nights can be published");

        Status = GameNightStatus.Published;
        UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var userId in invitedUserIds)
        {
            var rsvp = GameNightRsvp.Create(Id, userId);
            _rsvps.Add(rsvp);
        }

        AddDomainEvent(new GameNightPublishedEvent(Id, OrganizerId, Title, ScheduledAt, invitedUserIds));
    }

    /// <summary>
    /// Cancels the game night. Idempotent if already cancelled.
    /// Raises a GameNightCancelledEvent.
    /// </summary>
    public void Cancel()
    {
        ThrowIfCorrupted();

        if (Status == GameNightStatus.Cancelled)
            return; // idempotent

        if (Status == GameNightStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed game night");

        Status = GameNightStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;

        var invitedUserIds = _rsvps.Select(r => r.UserId).ToList();
        AddDomainEvent(new GameNightCancelledEvent(Id, OrganizerId, Title, invitedUserIds));
    }

    /// <summary>
    /// Marks the game night as completed. Only published events can be completed.
    /// </summary>
    public void Complete()
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.Published)
            throw new InvalidOperationException("Only published game nights can be completed");

        Status = GameNightStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds additional invitees to a published game night. Skips users already invited.
    /// Raises a GameNightPublishedEvent for the newly invited users only.
    /// </summary>
    public void AddInvitees(List<Guid> userIds)
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.Published)
            throw new InvalidOperationException("Can only invite to published game nights");

        var existingUserIds = _rsvps.Select(r => r.UserId).ToHashSet();
        var newUserIds = userIds.Where(id => !existingUserIds.Contains(id)).ToList();

        foreach (var userId in newUserIds)
        {
            var rsvp = GameNightRsvp.Create(Id, userId);
            _rsvps.Add(rsvp);
        }

        if (newUserIds.Count > 0)
        {
            AddDomainEvent(new GameNightPublishedEvent(Id, OrganizerId, Title, ScheduledAt, newUserIds));
        }
    }

    /// <summary>
    /// Pre-invites users during Draft creation. RSVPs are created with Pending status
    /// but no domain events are raised until Publish().
    /// </summary>
    public void PreInvite(List<Guid> userIds)
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.Draft)
            throw new InvalidOperationException("Can only pre-invite to draft game nights");

        foreach (var userId in userIds)
        {
            if (_rsvps.All(r => r.UserId != userId))
            {
                _rsvps.Add(GameNightRsvp.Create(Id, userId));
            }
        }
    }

    /// <summary>
    /// Gets the RSVP for a specific user, or null if not invited.
    /// </summary>
    public GameNightRsvp? GetRsvp(Guid userId) => _rsvps.FirstOrDefault(r => r.UserId == userId);

    /// <summary>
    /// Raises a GameNightRsvpReceivedEvent domain event for notification dispatch.
    /// </summary>
    public void AddRsvpReceivedEvent(Guid responderId, RsvpStatus response, Guid organizerId)
    {
        AddDomainEvent(new GameNightRsvpReceivedEvent(Id, responderId, response, organizerId));
    }

    /// <summary>
    /// Gets the number of accepted RSVPs.
    /// </summary>
    public int AcceptedCount => _rsvps.Count(r => r.Status == RsvpStatus.Accepted);

    /// <summary>
    /// Returns true if the game night has reached its maximum player count.
    /// </summary>
    public bool IsFull => MaxPlayers.HasValue && AcceptedCount >= MaxPlayers.Value;

    /// <summary>
    /// Marks that the 24-hour reminder has been sent.
    /// </summary>
    public void MarkReminder24hSent()
    {
        Reminder24hSentAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks that the 1-hour reminder has been sent.
    /// </summary>
    public void MarkReminder1hSent()
    {
        Reminder1hSentAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the RSVP deadline. Organiser-only operation. Per ADR-074 Option C,
    /// the deadline is consulted at validation time (lazy check); a Hangfire job
    /// observes expired deadlines and calls <see cref="MarkRsvpClosed"/> for audit.
    /// Pass null to clear (e.g. organiser extends an indefinitely open RSVP window).
    /// Throws if RSVP is already closed.
    /// </summary>
    public void SetRsvpDeadline(DateTimeOffset? deadline)
    {
        if (RsvpClosedAt.HasValue)
            throw new InvalidOperationException(
                "Cannot change RSVP deadline after RSVP has been closed");

        if (deadline.HasValue && deadline.Value <= DateTimeOffset.UtcNow)
            throw new ArgumentException(
                "RSVP deadline must be in the future",
                nameof(deadline));

        RsvpDeadline = deadline;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks RSVPs as closed. Internal entry point — callable only by the repository
    /// (during Hangfire cleanup job dispatch) or by validator on first-read deadline
    /// expiry observation, per ADR-074 Option C. Idempotent: subsequent calls are no-ops.
    /// </summary>
    internal void MarkRsvpClosed()
    {
        if (RsvpClosedAt.HasValue)
            return; // idempotent

        RsvpClosedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Returns true if RSVPs are no longer accepted (either explicit close, or
    /// deadline passed). Validator should call this before persisting a new RSVP.
    /// </summary>
    public bool IsRsvpClosed(DateTimeOffset utcNow)
    {
        if (RsvpClosedAt.HasValue)
            return true;
        if (RsvpDeadline.HasValue && utcNow >= RsvpDeadline.Value)
            return true;
        return false;
    }

    /// <summary>
    /// Internal method to restore RSVPs list from persistence.
    /// </summary>
    internal void RestoreRsvps(IEnumerable<GameNightRsvp> rsvps)
    {
        _rsvps.Clear();
        _rsvps.AddRange(rsvps);
    }

    // ── Sessions aggregate ──────────────────────────────────────────────

    private readonly List<GameNightSession> _sessions = new();
    public IReadOnlyList<GameNightSession> Sessions => _sessions.AsReadOnly();

    /// <summary>
    /// Returns the in-progress session, or the first pending session if none is active.
    /// </summary>
    public GameNightSession? CurrentSession =>
        _sessions.FirstOrDefault(s => s.Status == GameNightSessionStatus.InProgress)
        ?? _sessions.FirstOrDefault(s => s.Status == GameNightSessionStatus.Pending);

    /// <summary>
    /// Adds a game session to this game night. Max 5 sessions per event.
    ///
    /// <para>Allowed on <see cref="GameNightStatus.Published"/> (first sitting) and
    /// <see cref="GameNightStatus.InProgress"/> (SI-4 #2635: 2nd+ sitting resume — the night flips
    /// to InProgress via #15 after the first session starts). The max-1-live guard (#10) lives in
    /// <see cref="EnsureCanStartSession"/>/<see cref="StartCurrentSession"/>, so accepting an
    /// InProgress night here never mints a 2nd concurrent live session.</para>
    ///
    /// <para>A <see cref="GameNightStatus.Completed"/> night is terminal: resume-from-Completed is
    /// rejected here (mapped to 409 at the command boundary) with guidance to start a new session —
    /// SI-4 spec-panel: never resurrect a terminal state. Draft/Cancelled/Corrupted are likewise rejected.</para>
    /// </summary>
    public GameNightSession AddSession(Guid sessionId, Guid gameId, string gameTitle)
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.Published && Status != GameNightStatus.InProgress)
        {
            var message = Status == GameNightStatus.Completed
                ? "Cannot add sessions to a finalized game night — start a new session."
                : $"Cannot add sessions to a {Status} game night.";
            throw new InvalidOperationException(message);
        }
        if (_sessions.Count >= 5)
            throw new InvalidOperationException("A game night can have at most 5 sessions.");

        var playOrder = _sessions.Count + 1;
        var session = GameNightSession.Create(Id, sessionId, gameId, gameTitle, playOrder);
        _sessions.Add(session);
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new GameStartedInNightEvent(Id, sessionId, gameId, gameTitle, playOrder));

        return session;
    }

    /// <summary>
    /// WS1 DEC-4 — guard-before-create. Throws <see cref="MaxLiveSessionsExceededException"/>
    /// if any child session is already InProgress, WITHOUT mutating state. The start
    /// handler calls this before dispatching CreateSessionCommand, so a blocked 2nd-open
    /// returns 409 without minting an orphan tracking Session (StartCurrentSession's own
    /// guard fires only after the Session has already been created and committed).
    /// </summary>
    /// <exception cref="MaxLiveSessionsExceededException">
    /// Thrown when a session is already in InProgress status on this event.
    /// </exception>
    public void EnsureCanStartSession()
    {
        ThrowIfCorrupted();

        if (_sessions.Any(s => s.Status == GameNightSessionStatus.InProgress))
            throw new MaxLiveSessionsExceededException(Id);
    }

    /// <summary>
    /// Starts the first pending session.
    /// Enforces invariante #10 (GameNight/Session domain model): a GameNightEvent
    /// can have at most 1 GameNightSession in InProgress at a time.
    /// </summary>
    /// <exception cref="MaxLiveSessionsExceededException">
    /// Thrown when another session is already in InProgress status on this event.
    /// </exception>
    public void StartCurrentSession()
    {
        ThrowIfCorrupted();

        if (_sessions.Any(s => s.Status == GameNightSessionStatus.InProgress))
            throw new MaxLiveSessionsExceededException(Id);

        var session = _sessions.FirstOrDefault(s => s.Status == GameNightSessionStatus.Pending)
            ?? throw new InvalidOperationException("No pending session to start.");
        session.Start();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Completes the in-progress session, optionally recording a winner.
    /// </summary>
    public void CompleteCurrentSession(Guid? winnerId)
    {
        ThrowIfCorrupted();

        var session = _sessions.FirstOrDefault(s => s.Status == GameNightSessionStatus.InProgress)
            ?? throw new InvalidOperationException("No in-progress session to complete.");
        session.Complete(winnerId);
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new GameCompletedInNightEvent(Id, session.SessionId, session.GameId, session.GameTitle, winnerId));
    }

    /// <summary>
    /// Finalizes the game night, transitioning to Completed status.
    /// All sessions must be finished (not in-progress).
    /// </summary>
    public void FinalizeNight()
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.Published)
            throw new InvalidOperationException($"Cannot finalize a {Status} game night.");
        if (_sessions.Any(s => s.Status == GameNightSessionStatus.InProgress))
            throw new InvalidOperationException("Cannot finalize: a session is still in progress.");
        Status = GameNightStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new NightFinalizedEvent(Id, OrganizerId, Title, _sessions.Count));
    }

    /// <summary>
    /// Invariante #15 (Asse A semantic alignment, WP2 T3): trigger transition
    /// Published → InProgress when the first Session in the game night becomes live.
    ///
    /// <para>Behavior:
    /// <list type="bullet">
    ///   <item>Published → InProgress (typical first-session-started case).</item>
    ///   <item>InProgress → no-op (idempotent: AdHoc events start as InProgress, or
    ///         subsequent sessions started after the first don't transition again).</item>
    ///   <item>Other status (Draft/Cancelled/Completed/Corrupted) → throws.</item>
    /// </list></para>
    ///
    /// <para>Called by <c>GameManagement.SessionStartedHandler</c>
    /// (MediatR <c>INotificationHandler</c>) when a
    /// <see cref="Api.BoundedContexts.SessionTracking.Domain.Events.SessionStartedDomainEvent"/>
    /// is raised by <c>Session.OpenLiveMode()</c>.</para>
    /// </summary>
    /// <param name="sessionId">The Session aggregate ID that started (informational for events/logs).</param>
    /// <exception cref="InvalidOperationException">When status is Draft/Cancelled/Completed/Corrupted.</exception>
    public void HandleFirstSessionStarted(Guid sessionId)
    {
        ThrowIfCorrupted();

        if (Status == GameNightStatus.Published)
        {
            Status = GameNightStatus.InProgress;
            UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        if (Status == GameNightStatus.InProgress)
        {
            return; // idempotent
        }

        throw new InvalidOperationException(
            $"Cannot start session {sessionId} on GameNightEvent {Id}: status is {Status}. " +
            $"Invariant #15 requires Published or InProgress status.");
    }

    /// <summary>
    /// Internal method to restore sessions list from persistence.
    /// </summary>
    internal void RestoreSessions(IEnumerable<GameNightSession> sessions)
    {
        _sessions.Clear();
        _sessions.AddRange(sessions.OrderBy(s => s.PlayOrder));
    }

    /// <summary>
    /// Guards mutating operations from running on entities loaded with a corrupted persisted status.
    /// Such entities require manual intervention before any mutation can be applied.
    /// </summary>
    private void ThrowIfCorrupted()
    {
        if (Status == GameNightStatus.Corrupted)
            throw new InvalidOperationException(
                $"GameNightEvent {Id} is in Corrupted state. Manual intervention required before any mutation.");
    }
}
