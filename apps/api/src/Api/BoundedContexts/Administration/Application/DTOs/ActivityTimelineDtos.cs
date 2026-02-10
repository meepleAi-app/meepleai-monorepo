namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Base activity event for timeline aggregation (Issue #3973).
/// </summary>
public abstract record ActivityEvent
{
    public required Guid Id { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Type { get; init; }
}

/// <summary>
/// Event when a game is added to user's library.
/// </summary>
public record GameAddedEvent : ActivityEvent
{
    public required Guid GameId { get; init; }
    public required string GameName { get; init; }
    public string? CoverUrl { get; init; }
}

/// <summary>
/// Event when a game session is completed.
/// </summary>
public record SessionCompletedEvent : ActivityEvent
{
    public required Guid SessionId { get; init; }
    public required string GameName { get; init; }
    public int Duration { get; init; }
}

/// <summary>
/// Event when a chat conversation is saved.
/// </summary>
public record ChatSavedEvent : ActivityEvent
{
    public required Guid ChatId { get; init; }
    public required string Topic { get; init; }
}

/// <summary>
/// Event when a game is added to wishlist.
/// </summary>
public record WishlistAddedEvent : ActivityEvent
{
    public required Guid GameId { get; init; }
    public required string GameName { get; init; }
}

/// <summary>
/// Response DTO for activity timeline endpoint (Issue #3973).
/// </summary>
public record ActivityTimelineResponseDto(
    IReadOnlyList<ActivityEventDto> Activities,
    int TotalCount
);

/// <summary>
/// Flat activity event DTO for API response serialization.
/// Uses polymorphic nullable fields for different event types.
/// </summary>
public record ActivityEventDto(
    string Id,
    string Type,
    DateTime Timestamp,
    string? GameId,
    string? GameName,
    string? CoverUrl,
    string? SessionId,
    int? Duration,
    string? ChatId,
    string? Topic
)
{
    /// <summary>
    /// Creates a flat DTO from a typed ActivityEvent.
    /// </summary>
    public static ActivityEventDto FromEvent(ActivityEvent evt) => evt switch
    {
        GameAddedEvent e => new ActivityEventDto(
            e.Id.ToString(), e.Type, e.Timestamp,
            e.GameId.ToString(), e.GameName, e.CoverUrl,
            null, null, null, null),

        SessionCompletedEvent e => new ActivityEventDto(
            e.Id.ToString(), e.Type, e.Timestamp,
            null, e.GameName, null,
            e.SessionId.ToString(), e.Duration, null, null),

        ChatSavedEvent e => new ActivityEventDto(
            e.Id.ToString(), e.Type, e.Timestamp,
            null, null, null,
            null, null, e.ChatId.ToString(), e.Topic),

        WishlistAddedEvent e => new ActivityEventDto(
            e.Id.ToString(), e.Type, e.Timestamp,
            e.GameId.ToString(), e.GameName, null,
            null, null, null, null),

        _ => new ActivityEventDto(
            evt.Id.ToString(), evt.Type, evt.Timestamp,
            null, null, null, null, null, null, null)
    };
}
