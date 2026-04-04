using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
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
    /// Updates the game night event details. Cannot update cancelled or completed events.
    /// </summary>
    public void Update(string title, string? description, DateTimeOffset scheduledAt, string? location, int? maxPlayers, List<Guid>? gameIds)
    {
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
    /// Internal method to restore RSVPs list from persistence.
    /// </summary>
    internal void RestoreRsvps(IEnumerable<GameNightRsvp> rsvps)
    {
        _rsvps.Clear();
        _rsvps.AddRange(rsvps);
    }
}
