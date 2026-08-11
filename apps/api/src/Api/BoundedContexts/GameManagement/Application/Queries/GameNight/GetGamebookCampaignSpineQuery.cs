using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNight;

/// <summary>
/// #2632 (SI-1b, Phase 3): the spine read path. Given a libro-game campaign, returns the owning
/// GameNight "Serata" spine (title, organizer, status, session pip) plus the derived campaign
/// status — or <c>null</c> when the campaign has no GameNight-attached play (standalone, no spine).
/// </summary>
internal sealed record GetGamebookCampaignSpineQuery(Guid CampaignId, Guid CallerUserId)
    : IQuery<GamebookCampaignSpineDto?>;

/// <summary>
/// The "Serata" spine for a libro-game campaign. <see cref="CampaignStatus"/> is derived
/// ("InProgress" when a live sitting exists, else "Resumable"; "Completed" is a future manual
/// flag from SI-8). Session pip = <see cref="CompletedSessions"/> of <see cref="TotalSessions"/>.
/// </summary>
internal sealed record GamebookCampaignSpineDto(
    Guid GameNightId,
    string GameNightTitle,
    Guid OrganizerId,
    string GameNightStatus,
    int TotalSessions,
    int CompletedSessions,
    bool HasLiveSession,
    string CampaignStatus);
