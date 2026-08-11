namespace Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;

/// <summary>
/// DTO representing a single diary entry in a live game session.
/// Issue #2570 SP3 T4.
/// </summary>
internal record DiaryEntryDto(
    Guid Id,
    Guid AuthorId,
    DateTimeOffset CreatedAt,
    string Text
);
