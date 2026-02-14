using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Immutable event record for game analytics tracking.
/// Used to calculate trending scores based on user interactions.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
public sealed class GameAnalyticsEvent : Entity<Guid>
{
    private Guid _id;
    private readonly Guid _gameId;
    private readonly GameEventType _eventType;
    private readonly Guid? _userId;
    private readonly DateTime _timestamp;

    /// <summary>
    /// Gets the unique identifier of this event.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the game this event relates to.
    /// </summary>
    public Guid GameId => _gameId;

    /// <summary>
    /// Gets the type of event (Search, View, LibraryAdd, Play).
    /// </summary>
    public GameEventType EventType => _eventType;

    /// <summary>
    /// Gets the user who triggered the event (null for anonymous).
    /// </summary>
    public Guid? UserId => _userId;

    /// <summary>
    /// Gets the UTC timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp => _timestamp;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable S1144 // Unused private types or members should be removed - Required for EF Core
    private GameAnalyticsEvent() : base()
    {
        _timestamp = DateTime.UtcNow;
    }
#pragma warning restore S1144

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal GameAnalyticsEvent(
        Guid id,
        Guid gameId,
        GameEventType eventType,
        Guid? userId,
        DateTime timestamp) : base(id)
    {
        _id = id;
        _gameId = gameId;
        _eventType = eventType;
        _userId = userId;
        _timestamp = timestamp;
    }

    /// <summary>
    /// Records a new game analytics event.
    /// </summary>
    /// <param name="gameId">The game this event relates to.</param>
    /// <param name="eventType">The type of interaction.</param>
    /// <param name="userId">Optional user who triggered the event.</param>
    /// <returns>A new GameAnalyticsEvent instance.</returns>
    public static GameAnalyticsEvent Record(
        Guid gameId,
        GameEventType eventType,
        Guid? userId = null)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID is required.", nameof(gameId));

        if (!Enum.IsDefined(eventType))
            throw new ArgumentException("Invalid event type.", nameof(eventType));

        return new GameAnalyticsEvent(
            Guid.NewGuid(),
            gameId,
            eventType,
            userId,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Gets the weight multiplier for this event type.
    /// </summary>
    public int GetEventWeight()
    {
        return EventType switch
        {
            GameEventType.Search => 3,
            GameEventType.View => 1,
            GameEventType.LibraryAdd => 5,
            GameEventType.Play => 10,
            _ => 0
        };
    }

    /// <summary>
    /// Calculates the time-decayed score for this event.
    /// Uses exponential decay: weight * exp(-days_ago / 7)
    /// </summary>
    public double CalculateDecayedScore()
    {
        var daysAgo = (DateTime.UtcNow - Timestamp).TotalDays;
        var weight = GetEventWeight();
        return weight * Math.Exp(-daysAgo / 7.0);
    }
}
