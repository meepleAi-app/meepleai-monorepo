namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Wire shape returned by GamebookCampaignEndpoints. Issue #1392 introduced
/// <see cref="GameRefId"/> + <see cref="GameRefKind"/> so the FE can distinguish
/// shared vs private campaigns without hardcoding <c>kind: Shared</c>. Issue #1405
/// removed the legacy <c>GameId</c> alias after all FE consumers migrated to
/// <see cref="GameRefId"/>.
///
/// SI-8 (#2639): <see cref="Outcome"/> (int form of <c>GamebookCampaignOutcome</c>,
/// null = open/resumable) + <see cref="CompletedAt"/> expose the manual terminal
/// close, so the FE can render "completata"/"abbandonata" and filter the resume set.
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
    DateTimeOffset UpdatedAt,
    int? Outcome = null,
    DateTimeOffset? CompletedAt = null);
