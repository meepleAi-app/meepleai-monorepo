namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Counts displayed as badges on the admin KbSubNav.
/// Issue #1655 (F3-FU-6).
/// </summary>
public sealed record KbNavCountsDto(
    int ProcessingQueue,
    int Feedback7d,
    DateTimeOffset AsOf
);
