using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// SI-8 (#2639): terminally closes a libro-game campaign from the play-evening-end
/// 3-way selector. <c>Completa</c> → <see cref="GamebookCampaignOutcome.Completed"/>,
/// <c>Abbandona</c> → <see cref="GamebookCampaignOutcome.Abandoned"/>. "Archivia"
/// (resumable) does NOT dispatch this command — it just finalizes the evening's
/// Session (SI-3) and leaves the campaign open. Only the owner can close.
/// </summary>
public sealed record CloseGamebookCampaignCommand(
    Guid CampaignId,
    Guid CallerUserId,
    GamebookCampaignOutcome Outcome) : IRequest<GamebookCampaignDto>;
