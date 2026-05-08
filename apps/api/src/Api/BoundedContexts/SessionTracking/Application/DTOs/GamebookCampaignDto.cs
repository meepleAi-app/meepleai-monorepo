namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public sealed record GamebookCampaignDto(
    Guid Id,
    Guid GameId,
    Guid OwnerUserId,
    string Title,
    int CurrentParagraph,
    IReadOnlyList<int> History,
    DateTimeOffset LastReadAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
