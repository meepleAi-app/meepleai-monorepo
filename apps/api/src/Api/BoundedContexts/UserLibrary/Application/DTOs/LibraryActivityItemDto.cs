namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// Activity feed item for RecentActivityRail (Wave B.3 followup, Issue #642).
/// Aggregated from <c>UserLibraryEntries.AddedAt</c> + <c>StateChangedAt</c> as event proxy.
/// Limitation: only <c>added</c> and <c>state-changed</c> types — full event types
/// (<c>removed</c>, <c>session-recorded</c>) require dedicated DomainEventLog infrastructure
/// (out-of-scope here, tracked in a separate followup issue).
/// </summary>
internal record LibraryActivityItemDto(
    Guid Id,
    string Type,
    DateTime Timestamp,
    Guid GameId,
    string GameTitle,
    string Message
);
