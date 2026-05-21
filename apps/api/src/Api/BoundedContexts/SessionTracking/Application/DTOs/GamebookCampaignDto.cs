namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Wire shape returned by GamebookCampaignEndpoints. Issue #1392 added
/// <see cref="GameRefId"/> + <see cref="GameRefKind"/> so the FE can distinguish
/// shared vs private campaigns without hardcoding <c>kind: Shared</c>. The legacy
/// <see cref="GameId"/> field is retained as an alias for backward compatibility
/// (always equal to <see cref="GameRefId"/>) and will be removed once all FE callers
/// have migrated.
/// </summary>
public sealed record GamebookCampaignDto(
    Guid Id,
    Guid GameId,
    Guid GameRefId,
    int GameRefKind,
    Guid OwnerUserId,
    string Title,
    int CurrentParagraph,
    IReadOnlyList<int> History,
    DateTimeOffset LastReadAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
