namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Wire shape returned by GamebookCampaignEndpoints. Issue #1392 introduced
/// <see cref="GameRefId"/> + <see cref="GameRefKind"/> so the FE can distinguish
/// shared vs private campaigns without hardcoding <c>kind: Shared</c>. Issue #1405
/// removed the legacy <c>GameId</c> alias after all FE consumers migrated to
/// <see cref="GameRefId"/>.
/// </summary>
public sealed record GamebookCampaignDto(
    Guid Id,
    Guid GameRefId,
    int GameRefKind,
    Guid OwnerUserId,
    string Title,
    int CurrentParagraph,
    IReadOnlyList<int> History,
    DateTimeOffset LastReadAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
